namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// Why a Microsoft sign-in was refused. Each member names one check, so an application or an audit record can
    /// tell refusals apart without parsing a message. Intended for logs and audit records, not for the browser.
    /// </summary>
    /// <remarks>
    /// The sign-in endpoint records the member's name as <see cref="ExternalAuthResult.FailureDetail"/>. A failure
    /// of the state, the binding cookie, the code or the token is recorded as
    /// <see cref="ExternalAuthFailureReason.InvalidToken"/>; a genuine identity refused for its tenant, its
    /// authentication context or its sign-in time as <see cref="ExternalAuthFailureReason.Rejected"/>.
    /// <para>
    /// The member names are a stable contract: they are written into audit records that outlive any release, so a
    /// member is never renamed, and a new check gets a new member.
    /// </para>
    /// </remarks>
    public enum MicrosoftAuthFailureReason
    {
        /// <summary>
        /// The <c>state</c> is missing, not this server's, tampered with or otherwise unreadable.
        /// </summary>
        InvalidState,

        /// <summary>
        /// The <c>state</c> is genuine but older than <see cref="MicrosoftAuthOptions.StateLifetime"/>.
        /// </summary>
        ExpiredState,

        /// <summary>
        /// The browser completing the flow did not send the binding cookie the flow's Begin set: it is not the
        /// browser that began the flow, or the cookie has expired or been cleared.
        /// </summary>
        BindingCookieMissing,

        /// <summary>
        /// The browser sent a binding cookie for the flow, but not the value the flow's Begin set.
        /// </summary>
        BindingCookieMismatch,

        /// <summary>
        /// The code is missing, or Microsoft's token endpoint refused to redeem it (already used, expired, issued
        /// for another redirect URI or PKCE verifier, or a client credential Microsoft does not accept), or answered
        /// without an ID token.
        /// </summary>
        CodeRedemptionFailed,

        /// <summary>
        /// The ID token is malformed, not yet valid, has a <c>tid</c> that is not a GUID, lacks an <c>oid</c>, or has
        /// an <c>auth_time</c> that is not a number. (A token with no <c>tid</c> at all fails the issuer rule first,
        /// as <see cref="InvalidIssuer"/>.)
        /// </summary>
        InvalidToken,

        /// <summary>
        /// The ID token's signature does not verify against Microsoft's published signing keys, or it is signed with
        /// an algorithm other than RS256, the only one accepted.
        /// </summary>
        InvalidSignature,

        /// <summary>
        /// The ID token's audience is not <see cref="MicrosoftAuthOptions.ClientId"/>.
        /// </summary>
        InvalidAudience,

        /// <summary>
        /// The ID token's issuer is not the one for its own <c>tid</c>.
        /// </summary>
        InvalidIssuer,

        /// <summary>
        /// The ID token has expired.
        /// </summary>
        TokenExpired,

        /// <summary>
        /// The ID token's <c>nonce</c> is missing or is not the one the flow's Begin sent.
        /// </summary>
        NonceMismatch,

        /// <summary>
        /// The account is a personal Microsoft account. Only work and school accounts are accepted.
        /// </summary>
        PersonalAccount,

        /// <summary>
        /// The flow named a tenant, and the token is from another one.
        /// </summary>
        TenantMismatch,

        /// <summary>
        /// The token's tenant is not in <see cref="MicrosoftAuthOptions.AllowedTenantIds"/>.
        /// </summary>
        TenantNotAllowed,

        /// <summary>
        /// The flow required an authentication context, and the token's <c>acrs</c> does not contain it.
        /// </summary>
        MissingAuthenticationContext,

        /// <summary>
        /// The flow set a max authentication age, and the token carries no <c>auth_time</c>. The app registration
        /// must include <c>auth_time</c> as an optional ID token claim.
        /// </summary>
        MissingAuthTime,

        /// <summary>
        /// The flow set a max authentication age, and the token's <c>auth_time</c> is older than it.
        /// </summary>
        StaleAuthTime,

        /// <summary>
        /// The <c>state</c> was begun by another flow of the application (linking an account, a re-check at
        /// signing) and posted to the sign-in endpoint. Returned by the sign-in endpoint only.
        /// </summary>
        CallerBindingMismatch,
    }
}
