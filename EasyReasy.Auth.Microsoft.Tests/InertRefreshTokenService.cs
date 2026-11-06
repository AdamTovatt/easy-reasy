using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Microsoft.Tests
{
    /// <summary>
    /// An <see cref="IRefreshTokenService"/> used only to check that the sign-in endpoint passes a registered one to
    /// the handler. Nothing on that path calls it, so every member throws rather than fabricate a result.
    /// </summary>
    internal sealed class InertRefreshTokenService : IRefreshTokenService
    {
        public Task<string> CreateRefreshTokenAsync(string subject, string authType, string? serializedClaims, string? serializedRoles, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RefreshTokenCreationResult> CreateRefreshTokenWithFamilyAsync(string subject, string authType, string? serializedClaims, string? serializedRoles, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RefreshResult> RefreshAsync(string refreshToken, IJwtTokenService jwtTokenService, HttpContext? httpContext = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<LogoutResult> LogoutAsync(string? refreshToken, HttpContext? httpContext = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<SessionRevocationResult> InvalidateAllSessionsAsync(string subject, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task RetireFamilyAsync(string? familyId, string? subject = null, HttpContext? httpContext = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
