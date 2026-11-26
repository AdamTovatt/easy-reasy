namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The provider-neutral category of an <see cref="OpenIdConnectAuthFailureReason"/>, the one rule every sign-in
    /// endpoint on the flow records a refusal under, a provider package's included.
    /// </summary>
    public static class OpenIdConnectAuthFailureReasonExtensions
    {
        /// <summary>
        /// The category an audit record files a refusal under: a failure of the state, the binding cookie, the response,
        /// the provider, the code or the token is <see cref="ExternalAuthFailureReason.InvalidToken"/>; a genuine
        /// identity refused by the flow's rules is <see cref="ExternalAuthFailureReason.Rejected"/>.
        /// </summary>
        /// <param name="reason">The reason the flow gave.</param>
        /// <returns>The reason's category.</returns>
        /// <exception cref="ArgumentOutOfRangeException">A value that is no member, so a new member cannot ship
        /// unclassified.</exception>
        public static ExternalAuthFailureReason ToExternalAuthFailureReason(this OpenIdConnectAuthFailureReason reason)
        {
            return reason switch
            {
                OpenIdConnectAuthFailureReason.InvalidState
                    or OpenIdConnectAuthFailureReason.ExpiredState
                    or OpenIdConnectAuthFailureReason.BindingCookieMissing
                    or OpenIdConnectAuthFailureReason.BindingCookieMismatch
                    or OpenIdConnectAuthFailureReason.RedirectUriMismatch
                    or OpenIdConnectAuthFailureReason.IssuerParameterMismatch
                    or OpenIdConnectAuthFailureReason.IssuerParameterMissing
                    or OpenIdConnectAuthFailureReason.UnknownProvider
                    or OpenIdConnectAuthFailureReason.ProviderChanged
                    or OpenIdConnectAuthFailureReason.CodeRedemptionFailed
                    or OpenIdConnectAuthFailureReason.InvalidToken
                    or OpenIdConnectAuthFailureReason.InvalidSignature
                    or OpenIdConnectAuthFailureReason.InvalidAudience
                    or OpenIdConnectAuthFailureReason.InvalidIssuer
                    or OpenIdConnectAuthFailureReason.TokenExpired
                    or OpenIdConnectAuthFailureReason.NonceMismatch
                    or OpenIdConnectAuthFailureReason.CallerBindingMismatch => ExternalAuthFailureReason.InvalidToken,
                OpenIdConnectAuthFailureReason.IdentityMismatch
                    or OpenIdConnectAuthFailureReason.MissingAuthTime
                    or OpenIdConnectAuthFailureReason.StaleAuthTime
                    or OpenIdConnectAuthFailureReason.MissingAcr
                    or OpenIdConnectAuthFailureReason.AcrNotAccepted
                    or OpenIdConnectAuthFailureReason.MissingAmr
                    or OpenIdConnectAuthFailureReason.AmrNotAccepted
                    or OpenIdConnectAuthFailureReason.MissingRequiredClaim
                    or OpenIdConnectAuthFailureReason.RequiredClaimMismatch => ExternalAuthFailureReason.Rejected,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "The refusal reason has no category."),
            };
        }
    }
}
