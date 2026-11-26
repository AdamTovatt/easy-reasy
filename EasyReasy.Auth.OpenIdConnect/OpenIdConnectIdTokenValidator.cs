using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// Validates an ID token: its signature against the provider's published keys with an algorithm the provider allows,
    /// its audience and <c>azp</c>, its issuer, its lifetime on the registered clock, and its nonce. Then reads the
    /// identity out.
    /// </summary>
    /// <remarks>
    /// Checks the token, not the flow: the identity, sign-in time, <c>acr</c>, <c>amr</c> and required claim rules a flow
    /// was begun with are applied afterwards by <see cref="OpenIdConnectCodeFlow"/>.
    /// </remarks>
    internal sealed class OpenIdConnectIdTokenValidator
    {
        private readonly OpenIdConnectDiscovery _discovery;
        private readonly TimeProvider _timeProvider;
        private readonly JsonWebTokenHandler _tokenHandler = new JsonWebTokenHandler();

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenIdConnectIdTokenValidator"/> class.
        /// </summary>
        /// <param name="discovery">The discovery cache, for a fresh key set when a token names a key the cache lacks.</param>
        /// <param name="timeProvider">The clock a token's lifetime is checked against.</param>
        public OpenIdConnectIdTokenValidator(OpenIdConnectDiscovery discovery, TimeProvider timeProvider)
        {
            _discovery = discovery;
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Validates an ID token issued for the given nonce.
        /// </summary>
        /// <param name="providerKey">The provider's key, carried into the identity.</param>
        /// <param name="provider">The provider, for the client id, issuer and algorithms.</param>
        /// <param name="configuration">The provider's discovery document, for its keys.</param>
        /// <param name="idToken">The raw ID token from the token endpoint.</param>
        /// <param name="expectedNonce">The nonce from the flow's state.</param>
        /// <param name="cancellationToken">Cancels fetching the keys.</param>
        /// <returns>The identity, or why the token is not valid.</returns>
        /// <exception cref="InvalidOperationException">A fresh key set could not be retrieved.</exception>
        public async Task<OpenIdConnectIdTokenValidationResult> ValidateAsync(
            string providerKey,
            OpenIdConnectProvider provider,
            OpenIdConnectConfiguration configuration,
            string idToken,
            string expectedNonce,
            CancellationToken cancellationToken)
        {
            TokenValidationResult result = await _tokenHandler.ValidateTokenAsync(idToken, CreateParameters(provider, configuration));

            if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
            {
                // Possibly a key newer than the cached set: the provider has rotated its keys. The code is single-use, so
                // refusing would send the person back to the provider; fetch the keys afresh and validate once more before
                // refusing. Only a token the provider's token endpoint returned over the back channel reaches this point,
                // so a caller cannot make the server fetch at will.
                configuration = await _discovery.FetchAsync(provider, cancellationToken);
                result = await _tokenHandler.ValidateTokenAsync(idToken, CreateParameters(provider, configuration));
            }

            if (!result.IsValid)
            {
                return OpenIdConnectIdTokenValidationResult.Invalid(MapException(result.Exception));
            }

            return ReadIdentity(providerKey, provider, (JsonWebToken)result.SecurityToken, expectedNonce);
        }

        private TokenValidationParameters CreateParameters(OpenIdConnectProvider provider, OpenIdConnectConfiguration configuration)
        {
            TokenValidationParameters parameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidAudience = provider.ClientId,
                ValidateIssuer = true,
                IssuerSigningKeys = configuration.SigningKeys,
                RequireSignedTokens = true,
                ValidAlgorithms = provider.SigningAlgorithms,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                LifetimeValidator = TimeProviderLifetimeValidator.Create(_timeProvider),
            };

            if (provider.Issuer.IsTemplate)
            {
                parameters.IssuerValidator = (issuer, securityToken, _) => ValidateTemplateIssuer(provider.Issuer, issuer, securityToken);
            }
            else
            {
                parameters.ValidIssuer = provider.Issuer.Value;
            }

            return parameters;
        }

        /// <summary>
        /// The multi-tenant issuer rule, which the handler cannot apply itself since the discovery document names only the
        /// template.
        /// </summary>
        private static string ValidateTemplateIssuer(OpenIdConnectIssuer template, string issuer, SecurityToken securityToken)
        {
            if (securityToken is JsonWebToken token && template.IsTokenIssuer(issuer, token))
            {
                return issuer;
            }

            throw new SecurityTokenInvalidIssuerException($"The issuer '{issuer}' is not the issuer for the token's own tenant.")
            {
                InvalidIssuer = issuer,
            };
        }

        private static OpenIdConnectAuthFailureReason MapException(Exception? exception)
        {
            return exception switch
            {
                // A kid in neither the cached nor the fresh key set: the token is not signed by a published key.
                SecurityTokenSignatureKeyNotFoundException => OpenIdConnectAuthFailureReason.InvalidSignature,
                SecurityTokenInvalidSignatureException => OpenIdConnectAuthFailureReason.InvalidSignature,
                SecurityTokenInvalidAlgorithmException => OpenIdConnectAuthFailureReason.InvalidSignature,
                SecurityTokenInvalidAudienceException => OpenIdConnectAuthFailureReason.InvalidAudience,
                SecurityTokenInvalidIssuerException => OpenIdConnectAuthFailureReason.InvalidIssuer,
                SecurityTokenExpiredException => OpenIdConnectAuthFailureReason.TokenExpired,
                _ => OpenIdConnectAuthFailureReason.InvalidToken,
            };
        }

        private static OpenIdConnectIdTokenValidationResult ReadIdentity(string providerKey, OpenIdConnectProvider provider, JsonWebToken token, string expectedNonce)
        {
            // OpenID Connect Core 3.1.3.7: a token for several audiences must name this client as its authorized party,
            // and a token naming an authorized party must name this client.
            bool hasAuthorizedParty = token.TryGetPayloadValue("azp", out string? authorizedParty);
            if ((hasAuthorizedParty && !string.Equals(authorizedParty, provider.ClientId, StringComparison.Ordinal))
                || (!hasAuthorizedParty && token.Audiences.Count() > 1))
            {
                return OpenIdConnectIdTokenValidationResult.Invalid(OpenIdConnectAuthFailureReason.InvalidAudience);
            }

            if (!token.TryGetPayloadValue("nonce", out string? nonce) || nonce == null || !string.Equals(nonce, expectedNonce, StringComparison.Ordinal))
            {
                return OpenIdConnectIdTokenValidationResult.Invalid(OpenIdConnectAuthFailureReason.NonceMismatch);
            }

            if (!token.TryGetPayloadValue("sub", out string? subject) || string.IsNullOrWhiteSpace(subject))
            {
                return OpenIdConnectIdTokenValidationResult.Invalid(OpenIdConnectAuthFailureReason.InvalidToken);
            }

            DateTimeOffset? authenticationTime = null;
            if (token.TryGetClaim("auth_time", out Claim _))
            {
                if (!token.TryGetPayloadValue("auth_time", out long authTimeSeconds))
                {
                    return OpenIdConnectIdTokenValidationResult.Invalid(OpenIdConnectAuthFailureReason.InvalidToken);
                }

                authenticationTime = DateTimeOffset.FromUnixTimeSeconds(authTimeSeconds);
            }

            token.TryGetPayloadValue("acr", out string? authenticationContextClass);
            token.TryGetPayloadValue("email", out string? email);
            token.TryGetPayloadValue("name", out string? name);

            OpenIdConnectUserInfo userInfo = new OpenIdConnectUserInfo
            {
                ProviderKey = providerKey,
                Identity = new OpenIdConnectIdentity(token.Issuer, subject),
                AuthenticationTime = authenticationTime,
                Nonce = nonce,
                AuthenticationContextClass = authenticationContextClass,
                AuthenticationMethods = token.Claims.Where(claim => claim.Type == "amr").Select(claim => claim.Value).ToList(),
                Email = email,
                EmailVerified = ReadEmailVerified(token),
                Name = name,
                Claims = token.Claims.ToList(),
            };

            // The handler records the key the signature verified with on the token it validated.
            return OpenIdConnectIdTokenValidationResult.Valid(userInfo, OpenIdConnectPublicKey.ToJson(token.SigningKey));
        }

        /// <summary>
        /// Reads <c>email_verified</c>, which the specification makes a boolean but some providers send as the string
        /// <c>"true"</c>.
        /// </summary>
        private static bool ReadEmailVerified(JsonWebToken token)
        {
            if (token.TryGetPayloadValue("email_verified", out bool verified))
            {
                return verified;
            }

            return token.TryGetPayloadValue("email_verified", out string? verifiedText)
                && string.Equals(verifiedText, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
