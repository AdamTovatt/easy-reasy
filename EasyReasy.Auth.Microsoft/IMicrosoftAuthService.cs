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
        /// Begins a flow: builds Microsoft's authorize URL and the encrypted <c>state</c>, and binds the flow to the
        /// browser by setting a short-lived cookie on <paramref name="httpContext"/>'s response.
        /// </summary>
        /// <param name="request">What the flow asks of Microsoft and what its Complete will check.</param>
        /// <param name="httpContext">The request from the browser that will complete the flow; the binding cookie is
        /// set on its response.</param>
        /// <returns>The authorize URL to send the browser to, and the <c>state</c>.</returns>
        /// <exception cref="ArgumentException">
        /// The request is invalid: its redirect URI is not in <see cref="MicrosoftAuthOptions.RedirectUris"/>, its
        /// caller binding is null, its tenant id is the personal accounts tenant, its prompt is not a
        /// <see cref="MicrosoftAuthPrompt"/> member, its max authentication age is under one second, or its nonce or
        /// authentication context id is blank. Every problem is reported in one exception, and no cookie is set.
        /// </exception>
        MicrosoftAuthBeginResult Begin(MicrosoftAuthBeginRequest request, HttpContext httpContext);

        /// <summary>
        /// Completes a flow from the <c>code</c> and <c>state</c> Microsoft's redirect brought back: checks the state
        /// and the browser binding, clears the flow's cookie, redeems the code, validates the ID token, and applies
        /// the tenant, authentication context and max age checks the flow was begun with.
        /// </summary>
        /// <param name="code">The authorization code from the redirect.</param>
        /// <param name="state">The <c>state</c> from the redirect.</param>
        /// <param name="httpContext">The request from the browser completing the flow; its binding cookie is read
        /// from the request and cleared on the response.</param>
        /// <param name="cancellationToken">Cancels the calls to Microsoft.</param>
        /// <returns>The validated identity, the caller binding and the ID token it was read from, or the reason the flow
        /// was refused.</returns>
        /// <exception cref="HttpRequestException">
        /// Microsoft's token endpoint could not be reached, or answered with an unsuccessful status other than 400 or
        /// 401 (a server error, or a 429 throttle). An outage is not a refusal, so it is not reported as one.
        /// </exception>
        /// <exception cref="InvalidOperationException">Microsoft's signing keys could not be retrieved.</exception>
        Task<MicrosoftAuthCompletionResult> CompleteAsync(string code, string state, HttpContext httpContext, CancellationToken cancellationToken = default);
    }
}
