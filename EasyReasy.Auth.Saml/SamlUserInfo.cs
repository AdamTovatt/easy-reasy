namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// What a verified assertion says about the person, read from the assertion whose signature was verified.
    /// </summary>
    public sealed class SamlUserInfo
    {
        /// <summary>
        /// The account: link the application's user to this, all three parts, never to an email address. Its
        /// <see cref="SamlIdentity.IdentityProviderKey"/> is the key the sign-in was begun with.
        /// </summary>
        public required SamlIdentity Identity { get; init; }

        /// <summary>
        /// When the identity provider says the person authenticated (the assertion's <c>AuthnInstant</c>).
        /// </summary>
        public required DateTimeOffset AuthenticationInstant { get; init; }

        /// <summary>
        /// The authentication context class the identity provider reports, or null when it reports none. The identity
        /// provider's assertion, not proof that a factor ran for this sign-in.
        /// </summary>
        public string? AuthnContextClassRef { get; init; }

        /// <summary>
        /// The assertion's attributes by <c>Name</c>, each with its values in order. An attribute's value is what the
        /// identity provider asserts, such as a display name or an email address, and is never an identity.
        /// </summary>
        public required IReadOnlyDictionary<string, IReadOnlyList<string>> Attributes { get; init; }
    }
}
