using Microsoft.IdentityModel.JsonWebTokens;
using System.Diagnostics.CodeAnalysis;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// A provider's issuer and where its discovery document is read from, in one of two forms: a fixed issuer
    /// (<see cref="Fixed"/>), which is every provider an application configures, or a multi-tenant issuer template
    /// (<see cref="TenantTemplate"/>), for a provider package such as Microsoft Entra ID's <c>organizations</c> endpoint.
    /// </summary>
    /// <remarks>
    /// Each factory checks its own form and throws for a bad one, so an instance is always a usable issuer.
    /// </remarks>
    public sealed class OpenIdConnectIssuer
    {
        /// <summary>
        /// The placeholder an issuer template holds where a tenant's id goes.
        /// </summary>
        public const string TenantIdPlaceholder = "{tenantid}";

        private const string DiscoveryPath = "/.well-known/openid-configuration";

        private OpenIdConnectIssuer(string value, string metadataAddress, string? tenantClaim)
        {
            Value = value;
            MetadataAddress = metadataAddress;
            TenantClaim = tenantClaim;
        }

        /// <summary>
        /// The issuer identifier, or for a template the template, exactly as the discovery document's <c>issuer</c> names
        /// it.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// The address the discovery document is read from.
        /// </summary>
        public string MetadataAddress { get; }

        /// <summary>
        /// For a template, the ID token claim whose value replaces <see cref="TenantIdPlaceholder"/>; null for a fixed
        /// issuer.
        /// </summary>
        public string? TenantClaim { get; }

        /// <summary>
        /// Whether the issuer is a template, known per token only.
        /// </summary>
        internal bool IsTemplate => TenantClaim != null;

        /// <summary>
        /// A fixed issuer, for example <c>https://acme.okta.com</c>, whose discovery document is at the issuer followed by
        /// <c>/.well-known/openid-configuration</c>. The discovery document's <c>issuer</c> and every ID token's
        /// <c>iss</c> must equal it exactly.
        /// </summary>
        /// <param name="issuer">The issuer identifier: absolute https (http only on a loopback host: <c>localhost</c> or a
        /// loopback address, not a name under <c>localhost</c>, since the server fetches from it), without a query or
        /// fragment, and without <see cref="TenantIdPlaceholder"/>.</param>
        /// <returns>The issuer.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="issuer"/> is null.</exception>
        /// <exception cref="ArgumentException">The issuer is not such a URL, or holds the placeholder.</exception>
        public static OpenIdConnectIssuer Fixed(string issuer)
        {
            ArgumentNullException.ThrowIfNull(issuer);

            if (!IsIssuerUrl(issuer))
            {
                throw new ArgumentException($"The issuer '{issuer}' must be an absolute https URL (or http on a loopback host) without a query or fragment.", nameof(issuer));
            }

            // A template passed as a fixed issuer would build, then refuse every token as InvalidIssuer.
            if (issuer.Contains(TenantIdPlaceholder, StringComparison.Ordinal))
            {
                throw new ArgumentException($"The issuer '{issuer}' holds '{TenantIdPlaceholder}': a multi-tenant issuer is a {nameof(TenantTemplate)}.", nameof(issuer));
            }

            return new OpenIdConnectIssuer(issuer, $"{issuer.TrimEnd('/')}{DiscoveryPath}", null);
        }

        /// <summary>
        /// A multi-tenant issuer template, as Microsoft Entra ID's <c>organizations</c> endpoint names: the discovery
        /// document's <c>issuer</c> must equal the template, and an ID token's <c>iss</c> must be the template with the
        /// token's own <paramref name="tenantClaim"/> in place of <see cref="TenantIdPlaceholder"/>.
        /// </summary>
        /// <param name="template">The issuer template, holding <see cref="TenantIdPlaceholder"/> once; with a tenant in
        /// its place, an absolute https URL (http only on a loopback host) without a query or fragment.</param>
        /// <param name="tenantClaim">The ID token claim naming the token's tenant, for example <c>tid</c>.</param>
        /// <param name="metadataAddress">Where the discovery document is read from, since a template is no address:
        /// absolute https (http only on a loopback host).</param>
        /// <returns>The issuer.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">Any of the three breaks its rule, every problem reported at once.</exception>
        public static OpenIdConnectIssuer TenantTemplate(string template, string tenantClaim, string metadataAddress)
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(tenantClaim);
            ArgumentNullException.ThrowIfNull(metadataAddress);
            List<string> problems = new List<string>();

            int placeholder = template.IndexOf(TenantIdPlaceholder, StringComparison.Ordinal);
            if (placeholder < 0
                || template.IndexOf(TenantIdPlaceholder, placeholder + 1, StringComparison.Ordinal) >= 0
                || !IsIssuerUrl(template.Replace(TenantIdPlaceholder, "tenant", StringComparison.Ordinal)))
            {
                problems.Add($"The issuer template '{template}' must hold '{TenantIdPlaceholder}' once, and with a tenant in its place be an absolute https URL (or http on a loopback host) without a query or fragment.");
            }

            if (string.IsNullOrWhiteSpace(tenantClaim))
            {
                problems.Add("The tenant claim must not be blank.");
            }

            if (!OpenIdConnectAddress.TryParseFetchable(metadataAddress, out Uri? _))
            {
                problems.Add($"The metadata address '{metadataAddress}' must be an absolute https URL (or http on a loopback host).");
            }

            ArgumentProblems.ThrowIfAny(problems, "issuer template");

            return new OpenIdConnectIssuer(template, metadataAddress, tenantClaim);
        }

        /// <summary>
        /// Whether an issuer can be this one's: the fixed issuer itself, or for a template, the template with a tenant id
        /// in place of <see cref="TenantIdPlaceholder"/>.
        /// </summary>
        /// <param name="issuer">The issuer to check, or null, which no issuer is.</param>
        /// <returns>Whether it can be.</returns>
        internal bool CanIssueAs(string? issuer)
        {
            if (issuer == null)
            {
                return false;
            }

            return IsTemplate ? TryReadTenant(issuer, out string? _) : string.Equals(issuer, Value, StringComparison.Ordinal);
        }

        /// <summary>
        /// Whether an ID token's <c>iss</c> is the issuer for that token: the fixed issuer, or the template with the token's
        /// own tenant claim in place of <see cref="TenantIdPlaceholder"/>.
        /// </summary>
        /// <param name="issuer">The token's <c>iss</c>.</param>
        /// <param name="token">The token, for its tenant claim.</param>
        /// <returns>Whether it is.</returns>
        internal bool IsTokenIssuer(string issuer, JsonWebToken token)
        {
            if (TenantClaim == null)
            {
                return string.Equals(issuer, Value, StringComparison.Ordinal);
            }

            return token.TryGetPayloadValue(TenantClaim, out string? tenantId)
                && TryReadTenant(issuer, out string? issuerTenant)
                && string.Equals(issuerTenant, tenantId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Reads the tenant id out of an issuer that is this template with a tenant filled in. The one rule for a tenant
        /// id: non-empty, with no <c>/</c> (it is one path segment) and no brace (so the template itself is no tenant's
        /// issuer).
        /// </summary>
        private bool TryReadTenant(string issuer, [NotNullWhen(true)] out string? tenant)
        {
            int placeholder = Value.IndexOf(TenantIdPlaceholder, StringComparison.Ordinal);
            string prefix = Value[..placeholder];
            string suffix = Value[(placeholder + TenantIdPlaceholder.Length)..];
            tenant = null;

            if (issuer.Length <= prefix.Length + suffix.Length
                || !issuer.StartsWith(prefix, StringComparison.Ordinal)
                || !issuer.EndsWith(suffix, StringComparison.Ordinal))
            {
                return false;
            }

            string candidate = issuer[prefix.Length..^suffix.Length];
            if (candidate.IndexOfAny(['/', '{', '}']) >= 0)
            {
                return false;
            }

            tenant = candidate;
            return true;
        }

        /// <summary>
        /// The issuer, or for a template the template.
        /// </summary>
        /// <returns><see cref="Value"/>.</returns>
        public override string ToString()
        {
            return Value;
        }

        private static bool IsIssuerUrl(string? issuer)
        {
            return OpenIdConnectAddress.TryParseFetchable(issuer, out Uri? uri)
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment);
        }
    }
}
