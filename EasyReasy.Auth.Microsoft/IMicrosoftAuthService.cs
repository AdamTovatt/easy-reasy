using EasyReasy.Auth.OpenIdConnect;
using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The two steps of a Microsoft sign-in through the authorization code flow with PKCE. Registered by
    /// <c>AddEasyReasyMicrosoftAuth</c>. The package's own sign-in endpoints use it, and so can an application's
    /// authenticated endpoints that link an account or re-check who someone is at a sensitive moment.
    /// </summary>
    public interface IMicrosoftAuthService
    {
        /// <summary>
        /// Begins a flow: builds Microsoft's authorize URL for the flow's tenant segment and the encrypted <c>state</c>,
        /// and binds the flow to the browser by setting a short-lived cookie on <paramref name="httpContext"/>'s response.
        /// </summary>
        /// <param name="request">What the flow asks of Microsoft and what its completion will check.</param>
        /// <param name="httpContext">The request from the browser that will complete the flow; the binding cookie is
        /// set on its response.</param>
        /// <param name="cancellationToken">Cancels fetching Microsoft's OpenID configuration.</param>
        /// <returns>The authorize URL to send the browser to, and the <c>state</c>.</returns>
        /// <exception cref="ArgumentException">
        /// The request is invalid: its tenant id is the personal accounts tenant or its authentication context id is
        /// blank (both reported together first), or else its redirect URI is not in
        /// <see cref="MicrosoftAuthOptions.RedirectUris"/>, its caller binding is null, its prompt is not an
        /// <see cref="OpenIdConnectPrompt"/> member, its max authentication age is under one second, or its nonce is
        /// blank (reported together). No cookie is set.
        /// </exception>
        /// <exception cref="InvalidOperationException">Microsoft's OpenID configuration could not be retrieved.</exception>
        Task<OpenIdConnectAuthBeginResult> BeginAsync(MicrosoftAuthBeginRequest request, HttpContext httpContext, CancellationToken cancellationToken = default);

        /// <summary>
        /// Completes a flow from what Microsoft's redirect brought back: checks the state, the browser binding, the caller
        /// binding and the redirect URI the response arrived at, clears the flow's cookie, checks the <c>iss</c> parameter,
        /// redeems the code, validates the ID token, and applies the tenant, authentication context and max age checks the
        /// flow was begun with.
        /// </summary>
        /// <param name="response">The code, state and <c>iss</c> from the redirect, and the redirect URI it arrived at.</param>
        /// <param name="expectedCallerBinding">The <see cref="MicrosoftAuthBeginRequest.CallerBinding"/> this endpoint
        /// begins its flows with. A flow begun with another is refused as
        /// <see cref="OpenIdConnectAuthFailureReason.CallerBindingMismatch"/> before its code is redeemed, so a re-check
        /// endpoint never accepts a plain sign-in that carried none of its rules.</param>
        /// <param name="httpContext">The request from the browser completing the flow; its binding cookie is read
        /// from the request and cleared on the response.</param>
        /// <param name="cancellationToken">Cancels the calls to Microsoft.</param>
        /// <returns>The validated identity and the ID token it was read from, or the reason the flow was refused.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="HttpRequestException">
        /// Microsoft's token endpoint could not be reached, or answered with an unsuccessful status other than 400 or
        /// 401 (a server error, or a 429 throttle). An outage is not a refusal, so it is not reported as one.
        /// </exception>
        /// <exception cref="InvalidOperationException">Microsoft's OpenID configuration or signing keys could not be
        /// retrieved, or its token endpoint refused the client itself (such as <c>invalid_client</c> for an expired
        /// secret).</exception>
        Task<MicrosoftAuthCompletionResult> CompleteAsync(OpenIdConnectAuthorizationResponse response, string expectedCallerBinding, HttpContext httpContext, CancellationToken cancellationToken = default);
    }
}
