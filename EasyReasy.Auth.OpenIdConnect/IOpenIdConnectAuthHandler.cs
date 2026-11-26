using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// Consumer-implemented integration point for the sign-in endpoints that
    /// <see cref="OpenIdConnectAuthApplicationBuilderExtensions.AddOpenIdConnectAuthEndpoints"/> maps: the rules a sign-in
    /// with a provider is begun with, and what happens once an identity is verified (find the user linked to it, assign
    /// claims and roles, and issue a JWT).
    /// </summary>
    /// <remarks>
    /// Not needed by an application that only calls <see cref="IOpenIdConnectAuthService"/> from its own endpoints.
    /// </remarks>
    public interface IOpenIdConnectAuthHandler
    {
        /// <summary>
        /// Decides the redirect URI, prompt, max authentication age and accepted <c>acr</c> and <c>amr</c> values of a
        /// sign-in begun through <c>POST /api/auth/oidc/begin</c> with a provider. Called once per request, after the
        /// provider key resolved, so the rules can depend on the provider and the request.
        /// </summary>
        /// <remarks>
        /// Settings <see cref="OpenIdConnectCodeFlow.BeginAsync"/> refuses (a redirect URI that is not the provider's, an
        /// undefined <see cref="OpenIdConnectPrompt"/> value, a max age under one second, an empty or blank accepted value)
        /// are a configuration error: the begin request fails with its <see cref="ArgumentException"/>.
        /// </remarks>
        /// <param name="providerKey">The key of the provider the browser chose, which resolved to a provider.</param>
        /// <param name="httpContext">The begin request.</param>
        /// <returns>The sign-in's settings.</returns>
        Task<OpenIdConnectSignInSettings> GetSignInSettingsAsync(string providerKey, HttpContext httpContext);

        /// <summary>
        /// Handles a verified identity by finding the user linked to it and returning an <see cref="AuthResponse"/> with a
        /// JWT. Link by <see cref="OpenIdConnectUserInfo.Identity"/>, never by email.
        /// </summary>
        /// <param name="userInfo">The verified identity.</param>
        /// <param name="jwtTokenService">The JWT token service for creating access tokens.</param>
        /// <param name="refreshTokenService">The refresh token service, or null if refresh tokens are not configured.</param>
        /// <param name="httpContext">The sign-in request.</param>
        /// <returns>An <see cref="AuthResponse"/> if the user is accepted, or null to reject the sign-in.</returns>
        Task<AuthResponse?> HandleOpenIdConnectUserAsync(
            OpenIdConnectUserInfo userInfo,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService? refreshTokenService,
            HttpContext httpContext);
    }
}
