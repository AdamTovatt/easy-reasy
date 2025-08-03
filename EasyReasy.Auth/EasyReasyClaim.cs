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
    }
}