namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// Where an identity provider's assertions carry the person's stable identity: a persistent NameID, or one attribute
    /// whose value never changes for the person (an employee or object id). Never an email address, which a person can
    /// change and an identity provider can reassign. Built with <see cref="PersistentNameId"/> or
    /// <see cref="Attribute"/>.
    /// </summary>
    public sealed class SamlIdentitySource
    {
        /// <summary>
        /// The persistent NameID format: what <see cref="PersistentNameId"/> requires and AuthnRequests ask for.
        /// </summary>
        internal const string PersistentNameIdFormat = "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent";

        private SamlIdentitySource(string? attributeName)
        {
            AttributeName = attributeName;
        }

        /// <summary>
        /// The subject's NameID, which must have the persistent format. Its <c>NameQualifier</c> and
        /// <c>SPNameQualifier</c>, when present, must name the identity provider and this service provider, so they are
        /// checked rather than stored: an identity provider that starts or stops sending them does not change the account.
        /// AuthnRequests ask for a persistent NameID.
        /// </summary>
        public static SamlIdentitySource PersistentNameId { get; } = new SamlIdentitySource(null);

        /// <summary>
        /// The name of the attribute that carries the identity, or null when it is the persistent NameID.
        /// </summary>
        public string? AttributeName { get; }

        /// <summary>
        /// One attribute, which replaces the NameID: an assertion without it, or with more than one value for it, is
        /// refused. Use it for an identity provider that cannot send a persistent NameID.
        /// </summary>
        /// <param name="attributeName">The attribute's <c>Name</c>, exactly as the identity provider sends it.</param>
        /// <returns>The identity source.</returns>
        /// <exception cref="ArgumentException"><paramref name="attributeName"/> is null or blank.</exception>
        public static SamlIdentitySource Attribute(string attributeName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);
            return new SamlIdentitySource(attributeName);
        }

        /// <summary>
        /// The NameID format AuthnRequests ask for, or null when the identity is an attribute and any NameID will do.
        /// </summary>
        internal string? RequestedNameIdFormat => AttributeName == null ? PersistentNameIdFormat : null;

        /// <summary>
        /// Reads the subject from a verified assertion, or returns null when the assertion does not carry one this source
        /// accepts, or carries one longer than <see cref="SamlIdentity.MaxSubjectLength"/>.
        /// </summary>
        internal string? ReadSubject(SamlValidatedAssertion assertion, string serviceProviderEntityId)
        {
            string? subject = ReadAnySubject(assertion, serviceProviderEntityId);
            return subject?.Length <= SamlIdentity.MaxSubjectLength ? subject : null;
        }

        private string? ReadAnySubject(SamlValidatedAssertion assertion, string serviceProviderEntityId)
        {
            if (AttributeName != null)
            {
                if (!assertion.Attributes.TryGetValue(AttributeName, out IReadOnlyList<string>? values) || values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
                {
                    return null;
                }

                return values[0];
            }

            bool isPersistent = string.Equals(assertion.NameIdFormat, PersistentNameIdFormat, StringComparison.Ordinal);
            bool qualifiersMatch = (assertion.NameQualifier == null || string.Equals(assertion.NameQualifier, assertion.IdentityProviderEntityId, StringComparison.Ordinal))
                && (assertion.ServiceProviderNameQualifier == null || string.Equals(assertion.ServiceProviderNameQualifier, serviceProviderEntityId, StringComparison.Ordinal));
            if (!isPersistent || !qualifiersMatch || string.IsNullOrWhiteSpace(assertion.NameId))
            {
                return null;
            }

            return assertion.NameId;
        }
    }
}
