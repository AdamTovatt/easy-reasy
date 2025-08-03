using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Service for validating authentication requests and creating JWT tokens.
    /// </summary>
    public interface IAuthRequestValidationService
    {
        /// <summary>
        /// Validates an API key authentication request and returns a JWT token if valid.
        /// </summary>
        /// <param name="request">The API key authentication request.</param>
        /// <param name="jwtTokenService">The JWT token service for creating tokens.</param>
        /// <param name="httpContext">The HTTP context containing request information like headers and query parameters. Can be null for backward compatibility.</param>
        /// <returns>An AuthResponse with the JWT token if valid, null if invalid.</returns>
        Task<AuthResponse?> ValidateApiKeyRequestAsync(ApiKeyAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null);

        /// <summary>
        /// Validates a username/password authentication request and returns a JWT token if valid.
        /// </summary>
        /// <param name="request">The username/password authentication request.</param>
        /// <param name="jwtTokenService">The JWT token service for creating tokens.</param>
        /// <param name="httpContext">The HTTP context containing request information like headers and query parameters. Can be null for backward compatibility.</param>
        /// <returns>An AuthResponse with the JWT token if valid, null if invalid.</returns>
        Task<AuthResponse?> ValidateLoginRequestAsync(LoginAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null);
    }
} 