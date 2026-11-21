namespace EasyReasy.Auth
{
    /// <summary>
    /// Enum representing common claim types for EasyReasy.Auth.
    /// </summary>
    public enum EasyReasyClaim
    {
        /// <summary>
        /// User ID claim.
        /// </summary>
        UserId,

        /// <summary>
        /// Tenant ID claim.
        /// </summary>
        TenantId,

        /// <summary>
        /// Email claim.
        /// </summary>
        Email,

        /// <summary>
        /// Authentication type claim.
        /// </summary>
        AuthType,

        /// <summary>
        /// Issuer claim.
        /// </summary>
        Issuer,

        /// <summary>
        /// Refresh token family id claim (claim type <see cref="EasyReasyClaimTypes.RefreshFamilyId"/>). This is the client's own opaque
        /// session identifier: it is intentionally exposed in the signed, client-readable access token, and
        /// possessing it grants no ability to revoke the session — retirement is a server-only operation via
        /// <see cref="IRefreshTokenService.RetireFamilyAsync"/>.
        /// </summary>
        RefreshFamilyId,

        /// <summary>
        /// OAuth client id claim (claim type <see cref="EasyReasyClaimTypes.ClientId"/>), present only on a token the OAuth
        /// authorization server issued to a client.
        /// </summary>
        ClientId,
    }
}
