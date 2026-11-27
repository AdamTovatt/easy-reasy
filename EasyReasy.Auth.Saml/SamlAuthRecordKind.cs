namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// What a <see cref="SamlAuthRecord"/> holds. Store the number: the values are fixed, so stored rows keep their
    /// meaning across versions.
    /// </summary>
    public enum SamlAuthRecordKind
    {
        /// <summary>
        /// A sign-in begun and not yet answered, found by the hash of the AuthnRequest's ID, which the identity provider's
        /// response names in <c>InResponseTo</c>.
        /// </summary>
        PendingRequest = 1,

        /// <summary>
        /// The outcome of a response the assertion consumer service checked, kept until the completion page redeems it,
        /// found by the hash of the single-use code the browser was redirected with.
        /// </summary>
        Completion = 2,
    }
}
