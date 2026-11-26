using EasyReasy.Auth.Tests;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// An in-memory OpenID Connect provider on one host, reached through a <see cref="FakeNetwork"/>. It publishes a
    /// discovery document and a key set, and redeems the codes a test issues for an authorize URL, enforcing what a
    /// provider enforces on redemption: the client credentials sent the way the client is registered, the redirect URI
    /// the flow named and the PKCE verifier for the flow's challenge, once per code.
    /// </summary>
    /// <remarks>
    /// A multi-tenant issuer publishes an issuer template holding <c>{tenantid}</c>, as Microsoft Entra ID's
    /// <c>organizations</c> endpoint does, and signs each token with the issuer of the token's own <c>tid</c>.
    /// </remarks>
    internal sealed class FakeOpenIdConnectIssuer
    {
        public const string ClientId = "client-123";
        public const string ClientSecret = "secret with spaces & symbols";
        public const string Subject = "subject-ada";
        public const string TenantId = "tenant-a";

        private readonly FakeTimeProvider _clock;
        private readonly ConcurrentDictionary<string, PendingCode> _codes = new ConcurrentDictionary<string, PendingCode>();
        private readonly ConcurrentQueue<FakeTokenRequest> _tokenRequests = new ConcurrentQueue<FakeTokenRequest>();
        private readonly ConcurrentQueue<string> _redeemedIdTokens = new ConcurrentQueue<string>();
        private int _requestCount;
        private int _metadataRequestCount;
        private int _keysRequestCount;

        public FakeOpenIdConnectIssuer(FakeTimeProvider clock, string host, bool multiTenant = false)
        {
            _clock = clock;
            Host = host;
            BaseUrl = $"https://{host}";
            IsMultiTenant = multiTenant;
            Issuer = multiTenant ? $"{BaseUrl}/{OpenIdConnectIssuer.TenantIdPlaceholder}/v2.0" : BaseUrl;
            MetadataAddress = multiTenant ? $"{BaseUrl}/organizations/v2.0/.well-known/openid-configuration" : $"{BaseUrl}/.well-known/openid-configuration";
            DiscoveryIssuer = Issuer;
            AuthorizationEndpoint = $"{BaseUrl}/authorize";
            TokenEndpoint = $"{BaseUrl}/token";
            JwksUri = $"{BaseUrl}/keys";
            SigningKey = CreateRsaKey($"{host}-key");
            PublishedKeys = [SigningKey];
        }

        public string Host { get; }

        public string BaseUrl { get; }

        public bool IsMultiTenant { get; }

        /// <summary>
        /// The issuer a provider configures: the base URL, or the template for a multi-tenant issuer.
        /// </summary>
        public string Issuer { get; }

        public string MetadataAddress { get; }

        /// <summary>
        /// The <c>issuer</c> the discovery document names. A test changes it to publish a document for another issuer.
        /// </summary>
        public string DiscoveryIssuer { get; set; }

        public string AuthorizationEndpoint { get; set; }

        public string TokenEndpoint { get; set; }

        public string JwksUri { get; set; }

        /// <summary>
        /// Whether the discovery document says the provider sends the <c>iss</c> parameter (RFC 9207).
        /// </summary>
        public bool AdvertisesIssuerParameter { get; set; }

        /// <summary>
        /// How the client is registered to send its secret; the token endpoint refuses it sent any other way.
        /// </summary>
        public OpenIdConnectClientAuthentication ClientAuthentication { get; set; } = OpenIdConnectClientAuthentication.ClientSecretBasic;

        public SecurityKey SigningKey { get; }

        /// <summary>
        /// The keys the key set publishes. A test replaces it to rotate keys.
        /// </summary>
        public IReadOnlyList<SecurityKey> PublishedKeys { get; set; }

        /// <summary>
        /// When set, the token endpoint answers every request with this status, and <see cref="TokenEndpointErrorOverride"/>
        /// as its <c>error</c> when that is set too.
        /// </summary>
        public HttpStatusCode? TokenEndpointStatusOverride { get; set; }

        public string? TokenEndpointErrorOverride { get; set; }

        /// <summary>
        /// When set, the token endpoint answers every request with a 200 and this body.
        /// </summary>
        public string? TokenEndpointSuccessBodyOverride { get; set; }

        /// <summary>
        /// When set, the discovery document answers with this status and no body.
        /// </summary>
        public HttpStatusCode? MetadataStatusOverride { get; set; }

        public int RequestCount => _requestCount;

        public int MetadataRequestCount => _metadataRequestCount;

        public int KeysRequestCount => _keysRequestCount;

        public IReadOnlyList<FakeTokenRequest> TokenRequests => _tokenRequests.ToArray();

        /// <summary>
        /// Every ID token the token endpoint handed out for a redeemed code, in order.
        /// </summary>
        public IReadOnlyList<string> RedeemedIdTokens => _redeemedIdTokens.ToArray();

        /// <summary>
        /// The issuer this provider's tokens and <c>iss</c> parameter carry: the issuer, or for a multi-tenant issuer the
        /// one of <see cref="TenantId"/>.
        /// </summary>
        public string TokenIssuer => IssuerForTenant(TenantId);

        public string IssuerForTenant(string tenantId)
        {
            return Issuer.Replace(OpenIdConnectIssuer.TenantIdPlaceholder, tenantId, StringComparison.Ordinal);
        }

        public static RsaSecurityKey CreateRsaKey(string keyId)
        {
            return new RsaSecurityKey(RSA.Create(2048)) { KeyId = keyId };
        }

        public static ECDsaSecurityKey CreateEcKey(string keyId)
        {
            return new ECDsaSecurityKey(ECDsa.Create(ECCurve.NamedCurves.nistP256)) { KeyId = keyId };
        }

        /// <summary>
        /// Issues a code for the flow an authorize URL begins, as the provider would after the person signed in, and lets
        /// the test shape the ID token the code will redeem for. The defaults are a valid token for <see cref="Subject"/>
        /// carrying the flow's nonce, <c>auth_time</c> now, an <c>acr</c>, an <c>amr</c>, and a verified email.
        /// </summary>
        public string IssueCode(string authorizationUrl, Action<FakeIdTokenSettings>? customize = null)
        {
            Assert.IsTrue(authorizationUrl.StartsWith(AuthorizationEndpoint + "?", StringComparison.Ordinal), $"Not this issuer's authorize URL: {authorizationUrl}");
            Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(new Uri(authorizationUrl).Query);
            long now = _clock.GetUtcNow().ToUnixTimeSeconds();

            Dictionary<string, object> claims = new Dictionary<string, object>
            {
                ["iss"] = TokenIssuer,
                ["aud"] = ClientId,
                ["sub"] = Subject,
                ["nonce"] = query["nonce"].ToString(),
                ["iat"] = now,
                ["nbf"] = now,
                ["exp"] = now + 3600,
                ["auth_time"] = now,
                ["acr"] = "urn:mfa",
                ["amr"] = new[] { "pwd", "otp" },
                ["email"] = "ada@example.test",
                ["email_verified"] = true,
                ["name"] = "Ada Lovelace",
            };

            if (IsMultiTenant)
            {
                claims["tid"] = TenantId;
            }

            FakeIdTokenSettings settings = new FakeIdTokenSettings(claims, SigningKey);
            customize?.Invoke(settings);

            string code = "code-" + Guid.NewGuid().ToString("N");
            _codes[code] = new PendingCode(query["redirect_uri"].ToString(), query["code_challenge"].ToString(), CreateToken(settings));
            return code;
        }

        public async Task<HttpResponseMessage> HandleAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            string url = request.RequestUri!.GetLeftPart(UriPartial.Path);

            if (request.Method == HttpMethod.Get && url == MetadataAddress)
            {
                Interlocked.Increment(ref _metadataRequestCount);
                if (MetadataStatusOverride is HttpStatusCode metadataStatus)
                {
                    return new HttpResponseMessage(metadataStatus);
                }

                Dictionary<string, object> document = new Dictionary<string, object>
                {
                    ["issuer"] = DiscoveryIssuer,
                    ["authorization_endpoint"] = AuthorizationEndpoint,
                    ["token_endpoint"] = TokenEndpoint,
                    ["jwks_uri"] = JwksUri,
                    ["id_token_signing_alg_values_supported"] = new[] { "RS256", "ES256" },
                };

                if (AdvertisesIssuerParameter)
                {
                    document["authorization_response_iss_parameter_supported"] = true;
                }

                return Json(HttpStatusCode.OK, document);
            }

            if (request.Method == HttpMethod.Get && url == JwksUri)
            {
                Interlocked.Increment(ref _keysRequestCount);
                return Json(HttpStatusCode.OK, new Dictionary<string, object>
                {
                    ["keys"] = PublishedKeys.Select(ToPublicJwk).ToArray(),
                });
            }

            if (request.Method == HttpMethod.Post && url == TokenEndpoint)
            {
                string body = await request.Content!.ReadAsStringAsync(cancellationToken);
                Dictionary<string, string> form = QueryHelpers.ParseQuery(body).ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
                FakeTokenRequest tokenRequest = new FakeTokenRequest(form, request.Headers.Authorization?.ToString());
                _tokenRequests.Enqueue(tokenRequest);
                return RedeemCode(tokenRequest);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private HttpResponseMessage RedeemCode(FakeTokenRequest tokenRequest)
        {
            if (TokenEndpointStatusOverride is HttpStatusCode status)
            {
                return TokenEndpointErrorOverride == null
                    ? new HttpResponseMessage(status)
                    : Json(status, new Dictionary<string, object> { ["error"] = TokenEndpointErrorOverride });
            }

            if (TokenEndpointSuccessBodyOverride is string successBody)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(successBody, Encoding.UTF8, "application/json") };
            }

            if (!IsClientAuthenticated(tokenRequest))
            {
                return Json(HttpStatusCode.Unauthorized, new Dictionary<string, object> { ["error"] = "invalid_client" });
            }

            Dictionary<string, string> form = tokenRequest.Form;
            if (form.GetValueOrDefault("grant_type") != "authorization_code"
                || !_codes.TryRemove(form.GetValueOrDefault("code") ?? "", out PendingCode? pending)
                || pending.RedirectUri != form.GetValueOrDefault("redirect_uri")
                || pending.CodeChallenge != ComputeChallenge(form.GetValueOrDefault("code_verifier") ?? ""))
            {
                return Json(HttpStatusCode.BadRequest, new Dictionary<string, object> { ["error"] = "invalid_grant" });
            }

            _redeemedIdTokens.Enqueue(pending.IdToken);
            return Json(HttpStatusCode.OK, new Dictionary<string, object>
            {
                ["token_type"] = "Bearer",
                ["id_token"] = pending.IdToken,
                ["access_token"] = "unused-access-token",
                ["expires_in"] = 3600,
            });
        }

        /// <summary>
        /// Whether the client authenticated exactly the way it is registered to, and in no other way as well.
        /// </summary>
        private bool IsClientAuthenticated(FakeTokenRequest tokenRequest)
        {
            if (ClientAuthentication == OpenIdConnectClientAuthentication.ClientSecretPost)
            {
                return tokenRequest.Authorization == null
                    && tokenRequest.Form.GetValueOrDefault("client_id") == ClientId
                    && tokenRequest.Form.GetValueOrDefault("client_secret") == ClientSecret;
            }

            string expected = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{WebUtility.UrlEncode(ClientId)}:{WebUtility.UrlEncode(ClientSecret)}"));
            return tokenRequest.Authorization == $"Basic {expected}" && !tokenRequest.Form.ContainsKey("client_secret");
        }

        private static string CreateToken(FakeIdTokenSettings settings)
        {
            JsonWebTokenHandler handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };
            return handler.CreateToken(new SecurityTokenDescriptor
            {
                Claims = settings.Claims,
                SigningCredentials = new SigningCredentials(settings.SigningKey, settings.Algorithm),
            });
        }

        private static string ComputeChallenge(string verifier)
        {
            return Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        }

        private static Dictionary<string, object> ToPublicJwk(SecurityKey key)
        {
            if (key is ECDsaSecurityKey ecKey)
            {
                ECParameters parameters = ecKey.ECDsa.ExportParameters(includePrivateParameters: false);
                return new Dictionary<string, object>
                {
                    ["kty"] = "EC",
                    ["use"] = "sig",
                    ["kid"] = key.KeyId,
                    ["crv"] = "P-256",
                    ["x"] = Base64Url.EncodeToString(parameters.Q.X),
                    ["y"] = Base64Url.EncodeToString(parameters.Q.Y),
                };
            }

            RSAParameters rsaParameters = ((RsaSecurityKey)key).Rsa.ExportParameters(includePrivateParameters: false);
            return new Dictionary<string, object>
            {
                ["kty"] = "RSA",
                ["use"] = "sig",
                ["kid"] = key.KeyId,
                ["n"] = Base64Url.EncodeToString(rsaParameters.Modulus),
                ["e"] = Base64Url.EncodeToString(rsaParameters.Exponent),
            };
        }

        private static HttpResponseMessage Json(HttpStatusCode status, Dictionary<string, object> body)
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
        }

        private sealed record PendingCode(string RedirectUri, string CodeChallenge, string IdToken);
    }
}
