using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The bearer's audience check with the OAuth server registered. A connection's token (one carrying a
    /// <see cref="EasyReasyClaimTypes.ClientId"/> claim) is for the resource: it must carry
    /// <see cref="OAuthServerOptions.Resource"/> as its audience, and the request's endpoint must be open to connections
    /// (<see cref="AllowOAuthClientsAttribute"/>). Anywhere else the token is invalid, so a connection reaches only what
    /// the application opened to it, however the pipeline is wired. Every other token keeps the application's own
    /// audience rule.
    /// </summary>
    /// <remarks>
    /// It runs inside the bearer's token validation, so it holds whether or not the application calls
    /// <c>UseEasyReasyAuth</c>, and whether or not it configured an <c>Audience</c>. The endpoint is read from the
    /// request, so routing must run before authentication, as it does in a minimal-hosting app; with no endpoint chosen
    /// yet, a connection's token is refused.
    /// </remarks>
    internal sealed class OAuthConnectionAudienceValidator
    {
        private readonly string _resource;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AudienceValidator? _applicationValidator;

        private OAuthConnectionAudienceValidator(string resource, IHttpContextAccessor httpContextAccessor, AudienceValidator? applicationValidator)
        {
            _resource = resource;
            _httpContextAccessor = httpContextAccessor;
            _applicationValidator = applicationValidator;
        }

        /// <summary>
        /// Installs the check on the bearer's validation parameters, around any audience validator the application set.
        /// </summary>
        /// <param name="bearerOptions">The bearer options, after every <c>Configure</c>.</param>
        /// <param name="resource">The protected resource connection tokens are issued for.</param>
        /// <param name="httpContextAccessor">The accessor for the request being authenticated.</param>
        public static void Install(JwtBearerOptions bearerOptions, string resource, IHttpContextAccessor httpContextAccessor)
        {
            TokenValidationParameters parameters = bearerOptions.TokenValidationParameters;
            OAuthConnectionAudienceValidator validator = new OAuthConnectionAudienceValidator(resource, httpContextAccessor, parameters.AudienceValidator);
            parameters.AudienceValidator = validator.Validate;
        }

        /// <summary>
        /// Whether the bearer options still run this check, for the startup validation.
        /// </summary>
        /// <param name="bearerOptions">The final bearer options.</param>
        /// <returns>Whether the audience validator is this one.</returns>
        public static bool IsInstalled(JwtBearerOptions bearerOptions)
        {
            return bearerOptions.TokenValidationParameters?.AudienceValidator?.Target is OAuthConnectionAudienceValidator;
        }

        private bool Validate(IEnumerable<string> audiences, SecurityToken securityToken, TokenValidationParameters validationParameters)
        {
            if (!OAuthServerClaims.IsConnection(securityToken))
            {
                return _applicationValidator?.Invoke(audiences, securityToken, validationParameters) ?? MatchesApplicationAudience(audiences, validationParameters);
            }

            if (!audiences.Contains(_resource, StringComparer.Ordinal))
            {
                throw new SecurityTokenInvalidAudienceException("The OAuth connection's token was not issued for this resource.");
            }

            if (_httpContextAccessor.HttpContext?.GetEndpoint()?.Metadata.GetMetadata<AllowOAuthClientsAttribute>() == null)
            {
                throw new SecurityTokenInvalidAudienceException("This endpoint is not open to OAuth connections.");
            }

            return true;
        }

        // The rule the bearer applies without a validator of its own: no check when ValidateAudience is off, otherwise one
        // of the token's audiences must be a valid one.
        private static bool MatchesApplicationAudience(IEnumerable<string> audiences, TokenValidationParameters validationParameters)
        {
            if (!validationParameters.ValidateAudience)
            {
                return true;
            }

            IEnumerable<string?> validAudiences = (validationParameters.ValidAudiences ?? []).Append(validationParameters.ValidAudience);
            return audiences.Any(audience => validAudiences.Contains(audience, StringComparer.Ordinal));
        }
    }
}
