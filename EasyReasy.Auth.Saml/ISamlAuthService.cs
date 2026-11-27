using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The two steps of a SAML sign-in an application drives itself, with the identity providers its
    /// <see cref="ISamlIdentityProviderResolver"/> supplies and the sign-ins in progress kept in its
    /// <see cref="ISamlAuthStore"/>. Registered (scoped) by <c>AddEasyReasySamlAuth</c>. The package's own sign-in
    /// endpoints use it, and so can an application's authenticated endpoints that link an account or re-check who someone
    /// is at a sensitive moment.
    /// </summary>
    /// <remarks>
    /// Between the two steps the identity provider posts its response to the assertion consumer service that
    /// <see cref="SamlAuthApplicationBuilderExtensions.AddSamlAuthEndpoints"/> maps. That endpoint checks the response,
    /// keeps the outcome under a single-use code and redirects the browser to the begin request's
    /// <see cref="SamlAuthBeginRequest.CompletionPath"/> with it, where the application's page posts the code to the
    /// endpoint that calls <see cref="CompleteAsync"/>.
    /// </remarks>
    public interface ISamlAuthService
    {
        /// <summary>
        /// Begins a sign-in: resolves the identity provider, builds its AuthnRequest, keeps the sign-in in the store, and
        /// binds it to the browser by setting a short-lived cookie on <paramref name="httpContext"/>'s response.
        /// </summary>
        /// <param name="request">What the sign-in asks of the identity provider and what its completion will check.</param>
        /// <param name="httpContext">The request from the browser that will complete the sign-in; the binding cookie is
        /// set on its response.</param>
        /// <param name="cancellationToken">Cancels resolving the identity provider and storing the request.</param>
        /// <returns>The URL to send the browser to, or null when the identity provider key resolves to none (the key may
        /// come from the browser, so an unknown one is not an error).</returns>
        /// <exception cref="ArgumentException">The request is invalid: a blank key, a null caller binding, a completion
        /// path that is not a path on this site, a max authentication age that is not positive, an empty or blank accepted
        /// class, or an expected identity at another identity provider. Every problem knowable without the identity
        /// provider is reported in one exception, and no cookie is set.</exception>
        /// <exception cref="InvalidOperationException">The identity provider wants signed requests and
        /// <see cref="SamlAuthOptions.SigningCertificate"/> is not set.</exception>
        Task<SamlAuthBeginResult?> BeginAsync(SamlAuthBeginRequest request, HttpContext httpContext, CancellationToken cancellationToken = default);

        /// <summary>
        /// Completes a sign-in from the code the assertion consumer service redirected the browser with: redeems the code
        /// once, checks and clears the browser's binding cookie, and returns the outcome of the response.
        /// </summary>
        /// <param name="code">The completion code from the redirect, as the completion page posted it.</param>
        /// <param name="httpContext">The request from the browser completing the sign-in; its binding cookie is read from
        /// the request and cleared on the response.</param>
        /// <param name="cancellationToken">Cancels redeeming the code.</param>
        /// <returns>The verified user and the caller binding, or the reason the sign-in was refused.</returns>
        Task<SamlAuthCompletionResult> CompleteAsync(string code, HttpContext httpContext, CancellationToken cancellationToken = default);
    }
}
