using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The two steps of an OpenID Connect sign-in through the authorization code flow with PKCE, with the providers the
    /// application's <see cref="IOpenIdConnectProviderResolver"/> supplies. Registered by
    /// <c>AddEasyReasyOpenIdConnectAuth</c> as a scoped service, so the resolver may be scoped too (for example reading
    /// a database). The package's own sign-in endpoints use it, and so can an application's
    /// authenticated endpoints that link an account or re-check who someone is at a sensitive moment.
    /// </summary>
    public interface IOpenIdConnectAuthService
    {
        /// <summary>
        /// Begins a flow: resolves the provider, builds its authorize URL and the encrypted <c>state</c>, and binds the
        /// flow to the browser by setting a short-lived cookie on <paramref name="httpContext"/>'s response.
        /// </summary>
        /// <param name="request">What the flow asks of the provider and what its completion will check.</param>
        /// <param name="httpContext">The request from the browser that will complete the flow; the binding cookie is set
        /// on its response.</param>
        /// <param name="cancellationToken">Cancels resolving the provider and fetching its discovery document.</param>
        /// <returns>The authorize URL to send the browser to, and the <c>state</c>; or null when the provider key is blank
        /// or resolves to no provider (no cookie is set), so a key the browser chose cannot fail the request.</returns>
        /// <exception cref="ArgumentException">
        /// The request is invalid for the provider: a redirect URI that is not the provider's, a null caller binding, an
        /// undefined prompt, a max authentication age under one second, a blank nonce, an empty or blank accepted value, a
        /// required claim that is blank or reserved, or an expected identity at another issuer. Every problem is reported
        /// in one exception, and no cookie is set.
        /// </exception>
        /// <exception cref="InvalidOperationException">The provider's discovery document could not be retrieved or is not
        /// one it may use.</exception>
        Task<OpenIdConnectAuthBeginResult?> BeginAsync(OpenIdConnectAuthBeginRequest request, HttpContext httpContext, CancellationToken cancellationToken = default);

        /// <summary>
        /// Completes a flow from what the provider's redirect brought back: checks the state, the browser binding, the
        /// caller binding and the redirect URI the response arrived at, clears the flow's cookie, resolves the provider
        /// again, checks the <c>iss</c> parameter, redeems the code, validates the ID token, and applies the rules the flow
        /// was begun with.
        /// </summary>
        /// <param name="response">The code, state and <c>iss</c> from the redirect, and the redirect URI it arrived at.</param>
        /// <param name="expectedCallerBinding">The <see cref="OpenIdConnectAuthBeginRequest.CallerBinding"/> this endpoint
        /// begins its flows with. A flow begun with another is refused as
        /// <see cref="OpenIdConnectAuthFailureReason.CallerBindingMismatch"/> before its code is redeemed, so a re-check
        /// endpoint never accepts a plain sign-in that carried none of its rules.</param>
        /// <param name="httpContext">The request from the browser completing the flow; its binding cookie is read from the
        /// request and cleared on the response.</param>
        /// <param name="cancellationToken">Cancels the calls to the provider.</param>
        /// <returns>The validated identity and the ID token it was read from, or the reason the flow was refused.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="HttpRequestException">The provider's token endpoint could not be reached, or answered with an
        /// unsuccessful status other than 400 or 401 (a server error, or a 429 throttle). An outage is not a refusal, so
        /// it is not reported as one.</exception>
        /// <exception cref="InvalidOperationException">The provider's discovery document or keys could not be retrieved,
        /// or its token endpoint refused the client itself (such as <c>invalid_client</c> for an expired secret).</exception>
        Task<OpenIdConnectAuthCompletionResult> CompleteAsync(OpenIdConnectAuthorizationResponse response, string expectedCallerBinding, HttpContext httpContext, CancellationToken cancellationToken = default);
    }
}
