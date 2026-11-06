using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// Validates a Microsoft ID token: its signature against Microsoft's published signing keys, its audience, the
    /// issuer for its own tenant, its lifetime on the registered clock, and its nonce. Then reads the identity out.
    /// </summary>
    /// <remarks>
    /// Checks the token, not the flow: the tenant, authentication context and sign-in time rules a flow was begun
    /// with are applied afterwards by <see cref="MicrosoftAuthService"/>. Registered as a singleton, so the OpenID
    /// configuration and signing keys are cached and refreshed by one <see cref="ConfigurationManager{T}"/>.
    /// </remarks>
    internal sealed class MicrosoftIdTokenValidator
    {
        private readonly MicrosoftAuthOptions _options;
        private readonly MicrosoftAuthority _authority;
        private readonly TimeProvider _timeProvider;
        private readonly IConfigurationRetriever<OpenIdConnectConfiguration> _configurationRetriever;
        private readonly IDocumentRetriever _documentRetriever;
        private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
        private readonly JsonWebTokenHandler _tokenHandler = new JsonWebTokenHandler();

        /// <summary>
        /// Initializes a new instance of the <see cref="MicrosoftIdTokenValidator"/> class.
        /// </summary>
        /// <param name="options">The Microsoft options, for the client id.</param>
        /// <param name="authority">The authority, for the metadata address and the issuer rule.</param>
        /// <param name="httpClientFactory">Creates the package's named HTTP client, which fetches the metadata.</param>
        /// <param name="timeProvider">The clock a token's lifetime is checked against.</param>
        public MicrosoftIdTokenValidator(MicrosoftAuthOptions options, MicrosoftAuthority authority, IHttpClientFactory httpClientFactory, TimeProvider timeProvider)
        {
            _options = options;
            _authority = authority;
            _timeProvider = timeProvider;
            // One fetch path: the cache and the forced fetch after a missing key share both retrievers.
            _configurationRetriever = new OpenIdConnectConfigurationRetriever();
            _documentRetriever = new HttpClientFactoryDocumentRetriever(httpClientFactory);
            _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                _authority.MetadataAddress,
                _configurationRetriever,
                _documentRetriever);
        }

        /// <summary>
        /// Validates an ID token issued for the given nonce.
        /// </summary>
        /// <param name="idToken">The raw ID token from the token endpoint.</param>
        /// <param name="expectedNonce">The nonce from the flow's state.</param>
        /// <param name="cancellationToken">Cancels fetching the signing keys.</param>
        /// <returns>The identity, or why the token is not valid.</returns>
        /// <exception cref="InvalidOperationException">The signing keys could not be retrieved.</exception>
        public async Task<MicrosoftIdTokenValidationResult> ValidateAsync(string idToken, string expectedNonce, CancellationToken cancellationToken)
        {
            OpenIdConnectConfiguration configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);
            TokenValidationResult result = await _tokenHandler.ValidateTokenAsync(idToken, CreateParameters(configuration));

            if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
            {
                // Possibly a key newer than the cached set: Microsoft has rotated its keys. The code is single-use, so
                // refusing would send the user back to Microsoft; fetch the keys afresh and validate once more before
                // refusing. Once a ConfigurationManager holds a configuration, RequestRefresh updates it in the
                // background and GetConfigurationAsync keeps returning the held one, so asking it again now would hand
                // back the old set. RequestRefresh brings the cache up to date for later attempts, and this attempt
                // reads the metadata directly through the same retrievers. Only a token Microsoft's token endpoint
                // returned over the back channel reaches this point, so a caller cannot make the server fetch at will.
                _configurationManager.RequestRefresh();
                configuration = await FetchConfigurationAsync(cancellationToken);
                result = await _tokenHandler.ValidateTokenAsync(idToken, CreateParameters(configuration));
            }

            if (!result.IsValid)
            {
                return MicrosoftIdTokenValidationResult.Invalid(MapException(result.Exception));
            }

            return ReadIdentity((JsonWebToken)result.SecurityToken, expectedNonce);
        }

        /// <summary>
        /// Reads the OpenID configuration and signing keys from Microsoft now, bypassing the cache.
        /// </summary>
        /// <exception cref="InvalidOperationException">They could not be retrieved, as
        /// <see cref="ConfigurationManager{T}"/> reports it.</exception>
        private async Task<OpenIdConnectConfiguration> FetchConfigurationAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _configurationRetriever.GetConfigurationAsync(_authority.MetadataAddress, _documentRetriever, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Microsoft's signing keys could not be retrieved from '{_authority.MetadataAddress}'.", exception);
            }
        }

        private TokenValidationParameters CreateParameters(OpenIdConnectConfiguration configuration)
        {
            return new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidAudience = _options.ClientId,
                ValidateIssuer = true,
                IssuerValidator = ValidateIssuer,
                IssuerSigningKeys = configuration.SigningKeys,
                RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ValidateLifetime = true,
                RequireExpirationTime = true,
                LifetimeValidator = TimeProviderLifetimeValidator.Create(_timeProvider),
            };
        }

        /// <summary>
        /// The multi-tenant issuer rule: the issuer must be the one for the token's own <c>tid</c>. The
        /// <c>organizations</c> metadata names only a <c>{tenantid}</c> template, so it cannot be compared directly.
        /// </summary>
        private string ValidateIssuer(string issuer, SecurityToken securityToken, TokenValidationParameters validationParameters)
        {
            if (securityToken is JsonWebToken token
                && token.TryGetPayloadValue("tid", out string? tenantId)
                && !string.IsNullOrEmpty(tenantId)
                && string.Equals(issuer, _authority.GetIssuer(tenantId), StringComparison.Ordinal))
            {
                return issuer;
            }

            throw new SecurityTokenInvalidIssuerException($"The issuer '{issuer}' is not the issuer for the token's own tenant.")
            {
                InvalidIssuer = issuer,
            };
        }

        private static MicrosoftAuthFailureReason MapException(Exception? exception)
        {
            return exception switch
            {
                SecurityTokenInvalidSignatureException => MicrosoftAuthFailureReason.InvalidSignature,
                SecurityTokenInvalidAudienceException => MicrosoftAuthFailureReason.InvalidAudience,
                SecurityTokenInvalidIssuerException => MicrosoftAuthFailureReason.InvalidIssuer,
                SecurityTokenExpiredException => MicrosoftAuthFailureReason.TokenExpired,
                _ => MicrosoftAuthFailureReason.InvalidToken,
            };
        }

        private static MicrosoftIdTokenValidationResult ReadIdentity(JsonWebToken token, string expectedNonce)
        {
            if (!token.TryGetPayloadValue("nonce", out string? nonce) || nonce == null || !string.Equals(nonce, expectedNonce, StringComparison.Ordinal))
            {
                return MicrosoftIdTokenValidationResult.Invalid(MicrosoftAuthFailureReason.NonceMismatch);
            }

            // The issuer rule has already matched the issuer against this tid, so it is present; parsed once, here.
            if (!token.TryGetPayloadValue("tid", out string? tenantIdText) || !Guid.TryParse(tenantIdText, out Guid tenantId))
            {
                return MicrosoftIdTokenValidationResult.Invalid(MicrosoftAuthFailureReason.InvalidToken);
            }

            if (!token.TryGetPayloadValue("oid", out string? objectId) || string.IsNullOrWhiteSpace(objectId))
            {
                return MicrosoftIdTokenValidationResult.Invalid(MicrosoftAuthFailureReason.InvalidToken);
            }

            DateTimeOffset? authenticationTime = null;
            if (token.TryGetClaim("auth_time", out Claim _))
            {
                if (!token.TryGetPayloadValue("auth_time", out long authTimeSeconds))
                {
                    return MicrosoftIdTokenValidationResult.Invalid(MicrosoftAuthFailureReason.InvalidToken);
                }

                authenticationTime = DateTimeOffset.FromUnixTimeSeconds(authTimeSeconds);
            }

            token.TryGetPayloadValue("preferred_username", out string? preferredUsername);
            token.TryGetPayloadValue("name", out string? name);

            return MicrosoftIdTokenValidationResult.Valid(new MicrosoftUserInfo
            {
                TenantId = tenantId,
                ObjectId = objectId,
                AuthenticationTime = authenticationTime,
                Nonce = nonce,
                PreferredUsername = preferredUsername,
                Name = name,
                AuthenticationMethods = ReadValues(token, "amr"),
                AuthenticationContextIds = ReadValues(token, "acrs"),
            });
        }

        /// <summary>
        /// Reads a claim that Microsoft sends as an array of strings; a token without it yields an empty list.
        /// </summary>
        private static IReadOnlyList<string> ReadValues(JsonWebToken token, string claimType)
        {
            return token.Claims
                .Where(claim => claim.Type == claimType)
                .Select(claim => claim.Value)
                .ToList();
        }
    }
}
