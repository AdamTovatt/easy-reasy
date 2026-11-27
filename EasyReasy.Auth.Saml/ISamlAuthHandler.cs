using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// Consumer-implemented integration point for the sign-in endpoints that
    /// <see cref="SamlAuthApplicationBuilderExtensions.AddSamlAuthEndpoints"/> maps: the rules a sign-in with an identity
    /// provider is begun with, and what happens once a user is verified (find the user linked to the identity, assign
    /// claims and roles, and issue a JWT). The package never creates users.
    /// </summary>
    /// <remarks>
    /// Not needed by an application that only calls <see cref="ISamlAuthService"/> from its own endpoints.
    /// </remarks>
    public interface ISamlAuthHandler
    {
        /// <summary>
        /// Decides the completion page, <c>ForceAuthn</c>, max authentication age and accepted authentication context
        /// classes of a sign-in begun through <c>POST /api/auth/saml/begin</c>. Called once per request, after the identity
        /// provider key resolved, so the rules can depend on the identity provider and the request.
        /// </summary>
        /// <remarks>
        /// Settings <see cref="ISamlAuthService.BeginAsync"/> refuses are a configuration error: the begin request fails
        /// with its <see cref="ArgumentException"/>.
        /// </remarks>
        /// <param name="identityProviderKey">The key of the identity provider the browser chose, which resolved.</param>
        /// <param name="httpContext">The begin request.</param>
        /// <returns>The sign-in's settings.</returns>
        Task<SamlSignInSettings> GetSignInSettingsAsync(string identityProviderKey, HttpContext httpContext);

        /// <summary>
        /// Handles a verified user by finding the user linked to the identity and returning an <see cref="AuthResponse"/>
        /// with a JWT. Link by <see cref="SamlUserInfo.Identity"/>, never by an email attribute.
        /// </summary>
        /// <param name="userInfo">The verified user.</param>
        /// <param name="jwtTokenService">The JWT token service for creating access tokens.</param>
        /// <param name="refreshTokenService">The refresh token service, or null if refresh tokens are not configured.</param>
        /// <param name="httpContext">The sign-in request.</param>
        /// <returns>An <see cref="AuthResponse"/> if the user is accepted, or null to reject the sign-in.</returns>
        Task<AuthResponse?> HandleSamlUserAsync(
            SamlUserInfo userInfo,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService? refreshTokenService,
            HttpContext httpContext);
    }
}
