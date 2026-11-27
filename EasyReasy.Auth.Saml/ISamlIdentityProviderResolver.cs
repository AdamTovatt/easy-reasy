namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// Supplies the application's identity providers by a stable key the application chooses (for example a customer's
    /// id), so one can be added or its metadata refreshed without a restart. Registered by the application; it may be
    /// scoped, for example reading a database.
    /// </summary>
    /// <remarks>
    /// Called when a sign-in begins and again at the assertion consumer service. Each call should return the provider as
    /// configured now: the response is checked against the metadata of that moment, and a sign-in whose provider's entity
    /// id changed after it began is refused as <see cref="SamlAuthFailureReason.IdentityProviderChanged"/>.
    /// <para>
    /// Build each provider with the injected <see cref="SamlIdentityProviderFactory"/>, which keeps the metadata parsed,
    /// so building one per call is cheap. It throws for metadata the package cannot use; check metadata with
    /// <see cref="SamlIdentityProvider.FindMetadataProblems"/> when the application accepts it, so the resolver is never
    /// handed any. An exception from the resolver fails the request it serves, so a provider the application can no
    /// longer use should resolve to null instead.
    /// </para>
    /// <para>
    /// The key is part of every <see cref="SamlIdentity"/> the provider yields, because its configuration is what the
    /// response was verified against. Never give a key to another identity provider or customer, and never change the key
    /// of one in use, or the accounts linked through it stop matching.
    /// </para>
    /// </remarks>
    public interface ISamlIdentityProviderResolver
    {
        /// <summary>
        /// Finds the identity provider with the given key.
        /// </summary>
        /// <param name="identityProviderKey">The key. On the sign-in begin endpoint it comes from the browser, which
        /// chooses the identity provider to sign in with, so treat it as untrusted input.</param>
        /// <param name="cancellationToken">Cancels the lookup.</param>
        /// <returns>The identity provider, or null when none has that key.</returns>
        Task<SamlIdentityProvider?> FindIdentityProviderAsync(string identityProviderKey, CancellationToken cancellationToken);
    }
}
