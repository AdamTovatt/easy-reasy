namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// Why the Entra rules refused a Microsoft sign-in: a genuine account refused for its tenant or its authentication
    /// context. Every other refusal is the shared flow's <see cref="OpenIdConnect.OpenIdConnectAuthFailureReason"/>,
    /// passed through as <see cref="MicrosoftAuthCompletionResult.FlowFailureReason"/>, and so is a token whose
    /// <c>tid</c> or <c>oid</c> this package cannot read, as its <c>InvalidToken</c>. Intended for logs and audit
    /// records, not for the browser.
    /// </summary>
    /// <remarks>
    /// The sign-in endpoint records the member's name as <see cref="ExternalAuthResult.FailureDetail"/>, always as
    /// <see cref="ExternalAuthFailureReason.Rejected"/>, since each member refuses an account whose token validated.
    /// <para>
    /// The member names are a stable contract: they are written into audit records that outlive any release, so a
    /// member is never renamed, a new check gets a new member, and no member shares a name with a flow reason.
    /// </para>
    /// </remarks>
    public enum MicrosoftAuthFailureReason
    {
        /// <summary>
        /// The account is a personal Microsoft account. Only work and school accounts are accepted.
        /// </summary>
        PersonalAccount,

        /// <summary>
        /// The flow named a tenant, and the token, issued by that tenant, carries another tenant's <c>tid</c>. A token
        /// issued by another tenant is the flow's <see cref="OpenIdConnect.OpenIdConnectAuthFailureReason.InvalidIssuer"/>.
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
    }
}
