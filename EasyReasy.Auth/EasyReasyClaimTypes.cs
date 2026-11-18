namespace EasyReasy.Auth
{
    /// <summary>
    /// The claim types this library writes onto the tokens it issues and reads back from them. Read a claim through
    /// these constants, or through <see cref="EasyReasyClaim"/> and <see cref="HttpContextExtensions"/>, rather than
    /// spelling the name again.
    /// </summary>
    public static class EasyReasyClaimTypes
    {
        /// <summary>
        /// The refresh token family id (<c>"family_id"</c>). <see cref="IRefreshTokenService.StartSessionAsync"/> puts it on
        /// a session's first access token and <see cref="IRefreshTokenService.RefreshAsync"/> on every refreshed one,
        /// always from the server-side family id, so a value supplied in the claims is replaced. It is the client's own
        /// opaque session id: readable in the signed token, and granting no ability to revoke the session, which only
        /// the server does through <see cref="IRefreshTokenService.RetireFamilyAsync"/>.
        /// </summary>
        public const string RefreshFamilyId = "family_id";

        /// <summary>
        /// The tenant id (<c>"tenant_id"</c>), which <see cref="ClaimsInjectionMiddleware"/> copies into
        /// <c>HttpContext.Items</c> and <see cref="HttpContextExtensions.GetTenantId"/> reads.
        /// </summary>
        public const string TenantId = "tenant_id";

        /// <summary>
        /// The authentication type (<c>"auth_type"</c>), such as <c>"user"</c> or <c>"apikey"</c>, which
        /// <see cref="JwtTokenService"/> writes on every token and the <c>UserOnly</c> and <c>ApiKeyOnly</c> policies require.
        /// </summary>
        public const string AuthType = "auth_type";
    }
}
