namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// What a response that passed every check says, read from the assertion whose signature was verified.
    /// </summary>
    internal sealed class SamlValidatedAssertion
    {
        /// <summary>
        /// Gets the assertion's ID.
        /// </summary>
        public required string AssertionId { get; init; }

        /// <summary>
        /// Gets the entity id of the identity provider that issued the assertion.
        /// </summary>
        public required string IdentityProviderEntityId { get; init; }

        /// <summary>
        /// Gets the subject's NameID value, or <see langword="null"/> when the subject has none.
        /// </summary>
        public required string? NameId { get; init; }

        /// <summary>
        /// Gets the NameID's format, or <see langword="null"/> when it names none.
        /// </summary>
        public required string? NameIdFormat { get; init; }

        /// <summary>
        /// Gets the NameID's <c>NameQualifier</c>, or <see langword="null"/> when it has none.
        /// </summary>
        public required string? NameQualifier { get; init; }

        /// <summary>
        /// Gets the NameID's <c>SPNameQualifier</c>, or <see langword="null"/> when it has none.
        /// </summary>
        public required string? ServiceProviderNameQualifier { get; init; }

        /// <summary>
        /// Gets when the identity provider says the person authenticated (the AuthnStatement's <c>AuthnInstant</c>).
        /// </summary>
        public required DateTimeOffset AuthenticationInstant { get; init; }

        /// <summary>
        /// Gets the AuthnStatement's <c>AuthnContextClassRef</c>, or <see langword="null"/> when it names none.
        /// </summary>
        public required string? AuthnContextClassRef { get; init; }

        /// <summary>
        /// Gets the assertion's attributes by <c>Name</c>, each with its values in order.
        /// </summary>
        public required IReadOnlyDictionary<string, IReadOnlyList<string>> Attributes { get; init; }
    }
}
