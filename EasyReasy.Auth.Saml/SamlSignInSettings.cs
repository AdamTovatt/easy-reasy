namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The security-relevant inputs of a sign-in begun through <c>POST /api/auth/saml/begin</c>, decided per request by
    /// <see cref="ISamlAuthHandler.GetSignInSettingsAsync"/>. The browser's body may choose the identity provider and
    /// nothing else, so none of these can be chosen by the caller.
    /// </summary>
    public sealed class SamlSignInSettings
    {
        /// <summary>
        /// The application's sign-in completion page, as a path on this site. See
        /// <see cref="SamlAuthBeginRequest.CompletionPath"/>. The page posts the code it receives to
        /// <c>POST /api/auth/saml</c>.
        /// </summary>
        public required string CompletionPath { get; init; }

        /// <summary>
        /// Whether to send <c>ForceAuthn</c>. See <see cref="SamlAuthBeginRequest.ForceAuthentication"/>.
        /// </summary>
        public bool ForceAuthentication { get; init; }

        /// <summary>
        /// The longest time since the person authenticated that the sign-in accepts, or null for no limit. See
        /// <see cref="SamlAuthBeginRequest.MaxAuthenticationAge"/>.
        /// </summary>
        public TimeSpan? MaxAuthenticationAge { get; init; }

        /// <summary>
        /// The authentication context classes the sign-in accepts, or null. See
        /// <see cref="SamlAuthBeginRequest.AcceptedAuthnContextClassRefs"/>.
        /// </summary>
        public IReadOnlyList<string>? AcceptedAuthnContextClassRefs { get; init; }
    }
}
