using System.Diagnostics.CodeAnalysis;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The one place the Microsoft identity platform's URL shapes and fixed values are spelled: the authorize and
    /// token endpoints for a tenant, the metadata document the signing keys come from, the issuer a tenant's ID
    /// tokens carry, the scopes a flow asks for, and the personal accounts tenant. Registered once, built from
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
        /// The scopes a flow asks for and its code is redeemed for: an ID token and the profile claims, nothing for
        /// Microsoft Graph. Begin and the token request must send the same value.
        /// </summary>
        public const string Scope = "openid profile";

        /// <summary>
        /// The tenant id every personal Microsoft account (outlook.com, hotmail.com, Xbox) signs in under.
        /// </summary>
        public static readonly Guid PersonalAccountsTenantId = new Guid("9188040d-6c67-4c5b-b112-36a304b66dad");

        private readonly string _baseUrl;

        /// <summary>
        /// Initializes the authority from a base URL such as <c>https://login.microsoftonline.com</c>.
        /// </summary>
        /// <param name="baseUrl">The base URL, with or without a trailing slash.</param>
        public MicrosoftAuthority(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
        }

        /// <summary>
        /// The OpenID configuration of the <c>organizations</c> segment. Its signing keys are the ones every work
        /// or school tenant's ID tokens are signed with.
        /// </summary>
        public string MetadataAddress => $"{_baseUrl}/{OrganizationsSegment}/v2.0/.well-known/openid-configuration";

        /// <summary>
        /// The authority segment for a flow: the tenant id in its canonical lowercase hyphenated form, or
        /// <see cref="OrganizationsSegment"/> when the flow names no tenant.
        /// </summary>
        /// <param name="tenantId">The tenant the flow requires, or null.</param>
        /// <returns>The segment.</returns>
        public static string GetTenantSegment(Guid? tenantId) => tenantId?.ToString("D") ?? OrganizationsSegment;

        /// <summary>
        /// The authorize endpoint for a flow.
        /// </summary>
        /// <param name="tenantId">The tenant the flow requires, or null.</param>
        /// <returns>The endpoint URL.</returns>
        public string GetAuthorizeEndpoint(Guid? tenantId) => $"{_baseUrl}/{GetTenantSegment(tenantId)}/oauth2/v2.0/authorize";

        /// <summary>
        /// The token endpoint for a flow.
        /// </summary>
        /// <param name="tenantId">The tenant the flow requires, or null.</param>
        /// <returns>The endpoint URL.</returns>
        public string GetTokenEndpoint(Guid? tenantId) => $"{_baseUrl}/{GetTenantSegment(tenantId)}/oauth2/v2.0/token";

        /// <summary>
        /// The issuer an ID token from the given tenant carries. A multi-tenant application checks the issuer
        /// against the token's own <c>tid</c>, since the <c>organizations</c> metadata names only a template.
        /// </summary>
        /// <param name="tenantId">The token's <c>tid</c>, exactly as the token carries it.</param>
        /// <returns>The expected issuer.</returns>
        public string GetIssuer(string tenantId) => $"{_baseUrl}/{tenantId}/v2.0";

        /// <summary>
        /// Whether an endpoint address is one this package will talk to or send a browser to: absolute, and https or
        /// http on a loopback host (a local fake identity provider, or a redirect URI on a developer machine, which
        /// Microsoft also accepts over http).
        /// </summary>
        /// <param name="address">The address to check.</param>
        /// <param name="uri">The parsed address, when it is allowed.</param>
        /// <returns><c>true</c> when the address is allowed.</returns>
        public static bool TryParseAllowedAddress(string? address, [NotNullWhen(true)] out Uri? uri)
        {
            if (address != null
                && Uri.TryCreate(address, UriKind.Absolute, out Uri? parsed)
                && (parsed.Scheme == Uri.UriSchemeHttps || (parsed.Scheme == Uri.UriSchemeHttp && parsed.IsLoopback)))
            {
                uri = parsed;
                return true;
            }

            uri = null;
            return false;
        }
    }
}
