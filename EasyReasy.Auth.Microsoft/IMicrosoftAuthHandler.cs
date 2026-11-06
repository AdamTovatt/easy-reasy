using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// Consumer-implemented integration point for the sign-in endpoints that
    /// <see cref="MicrosoftAuthApplicationBuilderExtensions.AddMicrosoftAuthEndpoints"/> maps:
    /// the rules a sign-in is begun with, and what happens once a Microsoft identity is verified (find the user linked
    /// to it, assign claims and roles, and issue a JWT).
    /// </summary>
    /// <remarks>
    /// Not needed by an application that only calls <see cref="IMicrosoftAuthService"/> from its own endpoints.
    /// </remarks>
    public interface IMicrosoftAuthHandler
    {
        /// <summary>
        /// Decides the tenant, prompt, required authentication context, max authentication age and redirect URI of a
        /// sign-in begun through <c>POST /api/auth/microsoft/begin</c>. Called once per request, so the rules can
        /// depend on it (for example the tenant of the host the request came to).
        /// </summary>
        /// <remarks>
        /// Settings <see cref="IMicrosoftAuthService.Begin"/> refuses (a redirect URI outside
        /// <see cref="MicrosoftAuthOptions.RedirectUris"/>, the personal accounts tenant, an undefined
        /// <see cref="MicrosoftAuthPrompt"/> value, a blank authentication context id, a max age under one second)
        /// are a configuration error: the begin request fails with its <see cref="ArgumentException"/>.
        /// </remarks>
        /// <param name="httpContext">The begin request.</param>
        /// <returns>The sign-in's settings.</returns>
        Task<MicrosoftSignInSettings> GetSignInSettingsAsync(HttpContext httpContext);

        /// <summary>
        /// Handles a verified Microsoft identity by finding the user linked to it and returning an
        /// <see cref="AuthResponse"/> with a JWT. Link by <see cref="MicrosoftUserInfo.TenantId"/> and
        /// <see cref="MicrosoftUserInfo.ObjectId"/>, never by email.
        /// </summary>
        /// <param name="userInfo">The verified identity.</param>
        /// <param name="jwtTokenService">The JWT token service for creating access tokens.</param>
        /// <param name="refreshTokenService">The refresh token service, or null if refresh tokens are not configured.</param>
        /// <param name="httpContext">The sign-in request.</param>
        /// <returns>An <see cref="AuthResponse"/> if the user is accepted, or null to reject the sign-in.</returns>
        Task<AuthResponse?> HandleMicrosoftUserAsync(
            MicrosoftUserInfo userInfo,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService? refreshTokenService,
            HttpContext httpContext);
    }
}
