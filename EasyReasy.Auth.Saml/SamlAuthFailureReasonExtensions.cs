namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The provider-neutral category of a <see cref="SamlAuthFailureReason"/>, the one rule the sign-in endpoint records a
    /// refusal under, for an application's own endpoint on <see cref="ISamlAuthService"/> to audit the same way.
    /// </summary>
    public static class SamlAuthFailureReasonExtensions
    {
        /// <summary>
        /// The category an audit record files a refusal under: a failure of the code, the binding cookie, the identity
        /// provider or the response is <see cref="ExternalAuthFailureReason.InvalidToken"/>; a verified assertion refused
        /// by the sign-in's rules is <see cref="ExternalAuthFailureReason.Rejected"/>.
        /// </summary>
        /// <param name="reason">The reason the completion gave.</param>
        /// <returns>The reason's category.</returns>
        /// <exception cref="ArgumentOutOfRangeException">A value that is no member, so a new member cannot ship
        /// unclassified.</exception>
        public static ExternalAuthFailureReason ToExternalAuthFailureReason(this SamlAuthFailureReason reason)
        {
            return reason switch
            {
                SamlAuthFailureReason.UnknownCompletionCode
                    or SamlAuthFailureReason.BindingCookieMissing
                    or SamlAuthFailureReason.BindingCookieMismatch
                    or SamlAuthFailureReason.UnknownIdentityProvider
                    or SamlAuthFailureReason.IdentityProviderChanged
                    or SamlAuthFailureReason.InvalidResponse
                    or SamlAuthFailureReason.CallerBindingMismatch => ExternalAuthFailureReason.InvalidToken,
                SamlAuthFailureReason.MissingIdentity
                    or SamlAuthFailureReason.IdentityMismatch
                    or SamlAuthFailureReason.StaleAuthentication
                    or SamlAuthFailureReason.MissingAuthnContextClass
                    or SamlAuthFailureReason.AuthnContextClassNotAccepted => ExternalAuthFailureReason.Rejected,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "The refusal reason has no category."),
            };
        }
    }
}
