using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The <see cref="HttpContext.Items"/> keys under which <see cref="ClaimsInjectionMiddleware"/> stores what it read
    /// from the token and <see cref="HttpContextExtensions"/> reads it back, so the writer and the readers share one
    /// spelling.
    /// </summary>
    internal static class HttpContextItemKeys
    {
        /// <summary>The user id copied from the token's subject.</summary>
        public const string UserId = "UserId";

        /// <summary>The tenant id copied from the <see cref="EasyReasyClaimTypes.TenantId"/> claim.</summary>
        public const string TenantId = "TenantId";

        /// <summary>The token issuer, when custom middleware stores one.</summary>
        public const string Issuer = "Issuer";
    }
}
