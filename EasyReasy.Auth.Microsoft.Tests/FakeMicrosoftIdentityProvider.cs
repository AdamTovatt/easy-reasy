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

namespace EasyReasy.Auth.Microsoft.Tests
{
    /// <summary>
    /// An in-memory stand-in for the Microsoft identity platform, served through an <see cref="HttpMessageHandler"/>
    /// so no test touches the network. It publishes OpenID configuration and signing keys, and redeems the codes a
    /// test issues for an authorize URL, enforcing what Microsoft enforces on redemption: the client credentials, the
    /// redirect URI the flow named and the PKCE verifier for the flow's challenge, once per code.
    /// </summary>
    internal sealed class FakeMicrosoftIdentityProvider : HttpMessageHandler
    {
        public const string Authority = "https://login.fake.test";
        public const string ClientId = "11111111-aaaa-4aaa-8aaa-111111111111";
        public const string ClientSecret = "fake-client-secret";
        public const string TenantId = "22222222-bbbb-4bbb-8bbb-222222222222";
        public const string OtherTenantId = "33333333-cccc-4ccc-8ccc-333333333333";
        public const string PersonalAccountsTenantId = "9188040d-6c67-4c5b-b112-36a304b66dad";
        public const string ObjectId = "44444444-dddd-4ddd-8ddd-444444444444";

        public static readonly Guid Tenant = Guid.Parse(TenantId);
        public static readonly Guid OtherTenant = Guid.Parse(OtherTenantId);
        public static readonly Guid PersonalAccountsTenant = Guid.Parse(PersonalAccountsTenantId);

        private readonly FakeTimeProvider _clock;
        private readonly ConcurrentDictionary<string, PendingCode> _codes = new ConcurrentDictionary<string, PendingCode>();
        private readonly ConcurrentQueue<Dictionary<string, string>> _tokenRequests = new ConcurrentQueue<Dictionary<string, string>>();
        private int _metadataRequestCount;
        private int _keysRequestCount;

        public FakeMicrosoftIdentityProvider(FakeTimeProvider clock)
        {
            _clock = clock;
            SigningKey = CreateKey("published-key");
            PublishedKeys = [SigningKey];
        }

        /// <summary>
        /// The key tokens are signed with by default, published in the key set.
        /// </summary>
        public RsaSecurityKey SigningKey { get; }

        /// <summary>
        /// The keys the key set publishes. A test replaces it to rotate keys.
        /// </summary>
        public IReadOnlyList<RsaSecurityKey> PublishedKeys { get; set; }

        /// <summary>
        /// When set, the token endpoint answers every request with this status and no body.
        /// </summary>
        public HttpStatusCode? TokenEndpointStatusOverride { get; set; }

        /// <summary>
        /// When set, the token endpoint answers every request with a 200 and this body.
        /// </summary>
        public string? TokenEndpointSuccessBodyOverride { get; set; }

        /// <summary>
        /// When set, the OpenID configuration answers with this status and no body.
        /// </summary>
        public HttpStatusCode? MetadataStatusOverride { get; set; }

        /// <summary>
        /// The <c>jwks_uri</c> the OpenID configuration names. A test points it elsewhere, for example at http.
        /// </summary>
        public string JwksUri { get; set; } = $"{Authority}/organizations/discovery/v2.0/keys";

        public int MetadataRequestCount => _metadataRequestCount;

        public int KeysRequestCount => _keysRequestCount;

        public IReadOnlyList<Dictionary<string, string>> TokenRequests => _tokenRequests.ToArray();

        public static RsaSecurityKey CreateKey(string keyId)
        {
            return new RsaSecurityKey(RSA.Create(2048)) { KeyId = keyId };
        }

        /// <summary>
        /// Issues a code for the flow an authorize URL begins, as Microsoft would after the user signed in, and lets
        /// the test shape the ID token the code will redeem for. The defaults are a valid work account token for
        /// <see cref="TenantId"/> carrying the flow's nonce, <c>auth_time</c> now, and <c>amr</c> and <c>acrs</c>.
        /// </summary>
        public string IssueCode(string authorizationUrl, Action<FakeIdTokenSettings>? customize = null)
        {
            Uri uri = new Uri(authorizationUrl);
            Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(uri.Query);
            string tenantSegment = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)[0];
            long now = _clock.GetUtcNow().ToUnixTimeSeconds();
            string tenantId = tenantSegment == "organizations" ? TenantId : tenantSegment;

            Dictionary<string, object> claims = new Dictionary<string, object>
            {
                ["aud"] = ClientId,
                ["iss"] = $"{Authority}/{tenantId}/v2.0",
                ["tid"] = tenantId,
                ["oid"] = ObjectId,
                ["sub"] = "pairwise-subject",
                ["nonce"] = query["nonce"].ToString(),
                ["iat"] = now,
                ["nbf"] = now,
                ["exp"] = now + 3600,
                ["auth_time"] = now,
                ["name"] = "Ada Lovelace",
                ["preferred_username"] = "ada@contoso.example",
                ["amr"] = new[] { "pwd", "mfa" },
                ["acrs"] = new[] { "c1" },
            };

            FakeIdTokenSettings settings = new FakeIdTokenSettings(claims, SigningKey);
            customize?.Invoke(settings);

            string code = "code-" + Guid.NewGuid().ToString("N");
            _codes[code] = new PendingCode(
                tenantSegment,
                query["redirect_uri"].ToString(),
                query["code_challenge"].ToString(),
                CreateToken(settings));
            return code;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.GetLeftPart(UriPartial.Path);

            if (request.Method == HttpMethod.Get && url == $"{Authority}/organizations/v2.0/.well-known/openid-configuration")
            {
                Interlocked.Increment(ref _metadataRequestCount);
                if (MetadataStatusOverride is HttpStatusCode metadataStatus)
                {
                    return new HttpResponseMessage(metadataStatus);
                }

                return Json(HttpStatusCode.OK, new Dictionary<string, object>
                {
                    ["issuer"] = $"{Authority}/{{tenantid}}/v2.0",
                    ["authorization_endpoint"] = $"{Authority}/organizations/oauth2/v2.0/authorize",
                    ["token_endpoint"] = $"{Authority}/organizations/oauth2/v2.0/token",
                    ["jwks_uri"] = JwksUri,
                    ["id_token_signing_alg_values_supported"] = new[] { "RS256" },
                });
            }

            if (request.Method == HttpMethod.Get && url == JwksUri)
            {
                Interlocked.Increment(ref _keysRequestCount);
                return Json(HttpStatusCode.OK, new Dictionary<string, object>
                {
                    ["keys"] = PublishedKeys.Select(ToPublicJwk).ToArray(),
                });
            }

            if (request.Method == HttpMethod.Post && url.StartsWith(Authority + "/", StringComparison.Ordinal) && url.EndsWith("/oauth2/v2.0/token", StringComparison.Ordinal))
            {
                string tenantSegment = new Uri(url).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)[0];
                string body = await request.Content!.ReadAsStringAsync(cancellationToken);
                Dictionary<string, string> form = QueryHelpers.ParseQuery(body).ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
                _tokenRequests.Enqueue(form);
                return RedeemCode(tenantSegment, form);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        protected override void Dispose(bool disposing)
        {
            // IHttpClientFactory disposes its handler chain when the handler's lifetime ends. This handler is shared
            // by the test for its whole run, so it ignores that.
        }

        private HttpResponseMessage RedeemCode(string tenantSegment, Dictionary<string, string> form)
        {
            if (TokenEndpointStatusOverride is HttpStatusCode status)
            {
                return new HttpResponseMessage(status);
            }

            if (TokenEndpointSuccessBodyOverride is string body)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            }

            if (form.GetValueOrDefault("client_id") != ClientId || form.GetValueOrDefault("client_secret") != ClientSecret)
            {
                return Json(HttpStatusCode.Unauthorized, new Dictionary<string, object> { ["error"] = "invalid_client" });
            }

            if (form.GetValueOrDefault("grant_type") != "authorization_code"
                || !_codes.TryRemove(form.GetValueOrDefault("code") ?? "", out PendingCode? pending)
                || pending.TenantSegment != tenantSegment
                || pending.RedirectUri != form.GetValueOrDefault("redirect_uri")
                || pending.CodeChallenge != ComputeChallenge(form.GetValueOrDefault("code_verifier") ?? ""))
            {
                return Json(HttpStatusCode.BadRequest, new Dictionary<string, object> { ["error"] = "invalid_grant" });
            }

            return Json(HttpStatusCode.OK, new Dictionary<string, object>
            {
                ["token_type"] = "Bearer",
                ["id_token"] = pending.IdToken,
                ["access_token"] = "unused-access-token",
                ["expires_in"] = 3600,
            });
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

        private static Dictionary<string, object> ToPublicJwk(RsaSecurityKey key)
        {
            RSAParameters parameters = key.Rsa.ExportParameters(includePrivateParameters: false);
            return new Dictionary<string, object>
            {
                ["kty"] = "RSA",
                ["use"] = "sig",
                ["alg"] = "RS256",
                ["kid"] = key.KeyId,
                ["n"] = Base64Url.EncodeToString(parameters.Modulus),
                ["e"] = Base64Url.EncodeToString(parameters.Exponent),
            };
        }

        private static HttpResponseMessage Json(HttpStatusCode status, Dictionary<string, object> body)
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
        }

        private sealed record PendingCode(string TenantSegment, string RedirectUri, string CodeChallenge, string IdToken);
    }
}
