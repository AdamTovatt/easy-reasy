namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// Supplies the application's providers by a stable key the application chooses (for example <c>okta</c>, or a
    /// customer's id), so a provider can be added or changed without a restart. Registered by the application.
    /// </summary>
    /// <remarks>
    /// Called when a flow begins and again when it completes, since the client secret is never put in the <c>state</c>.
    /// Resolved from the request's services, so it may be scoped (for example reading a database). Each call should
    /// return the provider as configured now: a flow refuses completion as
    /// <see cref="OpenIdConnectAuthFailureReason.ProviderChanged"/> when the issuer or client id changed after it began.
    /// The discovery document and keys are cached per metadata address and issuer, so resolving does not refetch them.
    /// </remarks>
    public interface IOpenIdConnectProviderResolver
    {
        /// <summary>
        /// Finds the provider with the given key.
        /// </summary>
        /// <param name="providerKey">The key. On the sign-in begin endpoint it comes from the browser, which chooses the
        /// provider to sign in with, so treat it as untrusted input.</param>
        /// <param name="cancellationToken">Cancels the lookup.</param>
        /// <returns>The provider, or null when no provider has that key.</returns>
        Task<OpenIdConnectProvider?> FindProviderAsync(string providerKey, CancellationToken cancellationToken);
    }
}
