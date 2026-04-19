using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Service for validating authentication requests and creating JWT tokens.
    /// </summary>
    /// <remarks>
    /// Implementations must populate the <c>AttemptedSubject</c> / <c>AttemptedClientId</c> property on the returned
    /// result whenever it is knowable — including on failure for <see cref="LoginFailureReason.UnknownUser"/>
    /// or <see cref="ApiKeyAuthFailureReason.UnknownKey"/> — so that consumers can emit ISO 27001 A.12.4.1
    /// compliant audit records that attribute failed authentication attempts to an identifier.
    /// </remarks>
    public interface IAuthRequestValidationService
    {
        /// <summary>
        /// Validates an API key authentication request.
        /// </summary>
        /// <param name="request">The API key authentication request.</param>
        /// <param name="jwtTokenService">The JWT token service for creating tokens.</param>
        /// <param name="httpContext">The HTTP context containing request information like headers and query parameters. Can be null for non-HTTP validation flows.</param>
        /// <returns>
        /// An <see cref="ApiKeyAuthResult"/> describing success or failure. The result never carries the raw API key.
        /// </returns>
        Task<ApiKeyAuthResult> ValidateApiKeyRequestAsync(ApiKeyAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null);

        /// <summary>
        /// Validates a username/password authentication request.
        /// </summary>
        /// <param name="request">The username/password authentication request.</param>
        /// <param name="jwtTokenService">The JWT token service for creating tokens.</param>
        /// <param name="httpContext">The HTTP context containing request information like headers and query parameters. Can be null for non-HTTP validation flows.</param>
        /// <returns>
        /// A <see cref="LoginResult"/> describing success or failure. The result never carries the raw password.
        /// </returns>
        Task<LoginResult> ValidateLoginRequestAsync(LoginAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null);
    }
}
