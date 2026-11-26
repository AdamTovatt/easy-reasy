using EasyReasy.Auth.OpenIdConnect;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The one place the Microsoft identity platform's fixed values and issuer shapes are spelled: the tenant segment a
    /// flow uses, the issuer each segment's tokens carry and where its OpenID configuration is, the scopes a flow asks
    /// for, the Entra claims read from an ID token, and the personal accounts tenant. Built once from
    /// <see cref="MicrosoftAuthOptions.AuthorityBaseUrl"/>.
    /// </summary>
    internal sealed class MicrosoftAuthority
    {
        /// <summary>
        /// The tenant segment used when a flow names no tenant: any work or school account. Never <c>common</c>,
        /// which would also admit personal accounts at Microsoft's side.
        /// </summary>
        public const string OrganizationsSegment = "organizations";

        /// <summary>
        /// The scopes a flow asks for: an ID token and the profile claims, nothing for Microsoft Graph.
        /// </summary>
        public const string Scope = "openid profile";

        /// <summary>
        /// The ID token claim naming the account's tenant.
        /// </summary>
        public const string TenantClaim = "tid";

        /// <summary>
        /// The ID token claim naming the account's object id, unique within its tenant.
        /// </summary>
        public const string ObjectIdClaim = "oid";

        /// <summary>
        /// The ID token claim holding the account's sign-in name.
        /// </summary>
        public const string PreferredUsernameClaim = "preferred_username";

        /// <summary>
        /// The ID token claim a Conditional Access authentication context is reported in.
        /// </summary>
        public const string AuthenticationContextClaim = "acrs";

        /// <summary>
        /// The tenant id every personal Microsoft account (outlook.com, hotmail.com, Xbox) signs in under.
        /// </summary>
        public static readonly Guid PersonalAccountsTenantId = new Guid("9188040d-6c67-4c5b-b112-36a304b66dad");

        /// <summary>
        /// The format of a tenant id in a segment: lowercase, hyphenated, without braces.
        /// </summary>
        private const string TenantIdFormat = "D";

        private readonly string _baseUrl;

        /// <summary>
        /// Initializes the authority from a base URL such as <c>https://login.microsoftonline.com</c>.
        /// </summary>
        /// <param name="baseUrl">The base URL, with or without a trailing slash.</param>
        /// <exception cref="ArgumentException">The base URL is not an absolute https URL (http only on a loopback host)
        /// without a query or fragment.</exception>
        public MicrosoftAuthority(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            OrganizationsIssuer = OpenIdConnectIssuer.TenantTemplate(
                $"{_baseUrl}/{OpenIdConnectIssuer.TenantIdPlaceholder}/v2.0",
                TenantClaim,
                $"{_baseUrl}/{OrganizationsSegment}/v2.0/.well-known/openid-configuration");
        }

        /// <summary>
        /// The issuer for <see cref="OrganizationsSegment"/>: the multi-tenant template, whose tokens carry their own
        /// tenant's issuer.
        /// </summary>
        public OpenIdConnectIssuer OrganizationsIssuer { get; }

        /// <summary>
        /// The authority segment for a flow, which is also its provider key: the tenant id in its canonical lowercase
        /// hyphenated form, or <see cref="OrganizationsSegment"/> when the flow names no tenant.
        /// </summary>
        /// <param name="tenantId">The tenant the flow requires, or null.</param>
        /// <returns>The segment.</returns>
        public static string GetTenantSegment(Guid? tenantId) => tenantId?.ToString(TenantIdFormat) ?? OrganizationsSegment;

        /// <summary>
        /// The tenant a segment requires, or null for <see cref="OrganizationsSegment"/>.
        /// </summary>
        /// <param name="segment">A segment <see cref="GetTenantSegment"/> returned.</param>
        /// <returns>The tenant, or null.</returns>
        /// <exception cref="ArgumentException">The segment is neither.</exception>
        public static Guid? GetRequiredTenantId(string segment)
        {
            if (segment == OrganizationsSegment)
            {
                return null;
            }

            return TryReadTenantSegment(segment, out Guid tenantId)
                ? tenantId
                : throw new ArgumentException($"'{segment}' is not a tenant segment.", nameof(segment));
        }

        /// <summary>
        /// The issuer for a segment: for <see cref="OrganizationsSegment"/>, <see cref="OrganizationsIssuer"/>; for a
        /// tenant, that tenant's issuer, whose OpenID configuration names that tenant's authorize and token endpoints.
        /// Null for a segment that is neither.
        /// </summary>
        /// <param name="segment">The segment.</param>
        /// <returns>The issuer, or null.</returns>
        public OpenIdConnectIssuer? FindIssuer(string segment)
        {
            if (segment == OrganizationsSegment)
            {
                return OrganizationsIssuer;
            }

            return TryReadTenantSegment(segment, out Guid _) ? OpenIdConnectIssuer.Fixed($"{_baseUrl}/{segment}/v2.0") : null;
        }

        /// <summary>
        /// Reads a tenant segment, accepting only the canonical form <see cref="GetTenantSegment"/> produces, so one
        /// tenant has one provider key.
        /// </summary>
        private static bool TryReadTenantSegment(string segment, out Guid tenantId)
        {
            return Guid.TryParseExact(segment, TenantIdFormat, out tenantId) && segment == tenantId.ToString(TenantIdFormat);
        }
    }
}
