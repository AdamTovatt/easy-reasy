using Microsoft.IdentityModel.Tokens;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// One OpenID Connect provider the application signs people in with: its issuer and the client registered there.
    /// The authorize and token endpoints and the signing keys come from the issuer's discovery document, whose issuer
    /// must equal <see cref="Issuer"/>.
    /// </summary>
    /// <remarks>
    /// Returned by the application's <see cref="IOpenIdConnectProviderResolver"/> per request, so a provider can be added
    /// or changed without a restart. It is checked each time it is resolved; a provider breaking a rule is a
    /// configuration error, reported with every problem at once.
    /// </remarks>
    public sealed class OpenIdConnectProvider
    {
        /// <summary>
        /// The default <see cref="Scope"/>: an ID token, with the profile and email claims.
        /// </summary>
        public const string DefaultScope = "openid profile email";

        /// <summary>
        /// The placeholder a multi-tenant issuer template holds.
        /// </summary>
        public const string TenantIdPlaceholder = "{tenantid}";

        /// <summary>
        /// The issuer identifier, for example <c>https://acme.okta.com</c>. The discovery document's <c>issuer</c> and
        /// every ID token's <c>iss</c> must equal it exactly, except with a <see cref="TenantClaim"/>: it is then the
        /// template the discovery document names, and a token's <c>iss</c> is the template with its own tenant filled in.
        /// Absolute https (http only on a loopback host), without a query or fragment.
        /// </summary>
        public required string Issuer { get; init; }

        /// <summary>
        /// The client id registered at the provider. An ID token's audience must hold it.
        /// </summary>
        public required string ClientId { get; init; }

        /// <summary>
        /// The client secret, sent to the token endpoint as <see cref="ClientAuthentication"/> says. It never reaches the
        /// browser.
        /// </summary>
        public required string ClientSecret { get; init; }

        /// <summary>
        /// The redirect URIs a flow with this provider may name, each registered at the provider. Give every provider
        /// redirect URIs of its own, never shared with another provider: completion refuses a response that arrived at
        /// another redirect URI than the flow named, which is what stops a provider from passing off another's
        /// response (a mix-up), and that only holds while no two providers share one. Matching is exact.
        /// </summary>
        public required IReadOnlyList<string> RedirectUris { get; init; }

        /// <summary>
        /// How the client secret is sent to the token endpoint. Defaults to
        /// <see cref="OpenIdConnectClientAuthentication.ClientSecretBasic"/>.
        /// </summary>
        public OpenIdConnectClientAuthentication ClientAuthentication { get; init; } = OpenIdConnectClientAuthentication.ClientSecretBasic;

        /// <summary>
        /// The algorithms an ID token may be signed with, each asymmetric (RS, PS or ES at 256, 384 or 512). Defaults to
        /// RS256 alone, which every provider supports. A token signed otherwise is refused.
        /// </summary>
        public IReadOnlyList<string> SigningAlgorithms { get; init; } = [SecurityAlgorithms.RsaSha256];

        /// <summary>
        /// The space-separated scopes a flow asks for. Must hold <c>openid</c>. Defaults to <see cref="DefaultScope"/>.
        /// </summary>
        public string Scope { get; init; } = DefaultScope;

        /// <summary>
        /// Where the discovery document is read from, or null for <see cref="Issuer"/> followed by
        /// <c>/.well-known/openid-configuration</c>. Its issuer must still equal <see cref="Issuer"/>.
        /// </summary>
        /// <remarks>
        /// For a provider package whose issuer cannot be the address, such as Microsoft Entra ID's multi-tenant endpoint
        /// (see <see cref="TenantClaim"/>). An application leaves it null.
        /// </remarks>
        public string? MetadataAddress { get; init; }

        /// <summary>
        /// For a multi-tenant provider whose discovery document names an issuer template holding <c>{tenantid}</c>, as
        /// Microsoft Entra ID's <c>organizations</c> endpoint does: the ID token claim whose value replaces
        /// <c>{tenantid}</c> to give the issuer that token must carry. <see cref="Issuer"/> is then that template, and
        /// <see cref="MetadataAddress"/> must be set. Null, the default, for every other provider.
        /// </summary>
        public string? TenantClaim { get; init; }

        /// <summary>
        /// The address the discovery document is read from.
        /// </summary>
        internal string ResolvedMetadataAddress => MetadataAddress ?? $"{Issuer.TrimEnd('/')}/.well-known/openid-configuration";

        /// <summary>
        /// Whether an issuer can be this provider's: <see cref="Issuer"/> itself, or with a <see cref="TenantClaim"/>, the
        /// template with a tenant (non-empty, with no <c>/</c>) in place of <c>{tenantid}</c>.
        /// </summary>
        /// <param name="issuer">The issuer to check.</param>
        /// <returns>Whether it can be.</returns>
        internal bool CanIssueAs(string? issuer)
        {
            if (issuer == null)
            {
                return false;
            }

            if (TenantClaim == null)
            {
                return string.Equals(issuer, Issuer, StringComparison.Ordinal);
            }

            int placeholder = Issuer.IndexOf(TenantIdPlaceholder, StringComparison.Ordinal);
            string prefix = Issuer[..placeholder];
            string suffix = Issuer[(placeholder + TenantIdPlaceholder.Length)..];

            if (issuer.Length <= prefix.Length + suffix.Length
                || !issuer.StartsWith(prefix, StringComparison.Ordinal)
                || !issuer.EndsWith(suffix, StringComparison.Ordinal))
            {
                return false;
            }

            string tenant = issuer[prefix.Length..^suffix.Length];
            return !tenant.Contains('/');
        }

        /// <summary>
        /// Every problem with this provider, for the flow to report in one exception.
        /// </summary>
        /// <returns>One message per problem; empty when the provider is valid.</returns>
        internal IReadOnlyList<string> FindProblems()
        {
            List<string> problems = new List<string>();
            bool isTemplate = TenantClaim != null;

            if (!OpenIdConnectAddress.TryParseFetchable(isTemplate ? Issuer?.Replace(TenantIdPlaceholder, "tenant", StringComparison.Ordinal) : Issuer, out Uri? issuer)
                || !string.IsNullOrEmpty(issuer.Query)
                || !string.IsNullOrEmpty(issuer.Fragment))
            {
                problems.Add($"{nameof(Issuer)} '{Issuer}' must be an absolute https URL (or http on a loopback host) without a query or fragment.");
            }

            if (string.IsNullOrWhiteSpace(ClientId))
            {
                problems.Add($"{nameof(ClientId)} must not be blank.");
            }

            if (string.IsNullOrWhiteSpace(ClientSecret))
            {
                problems.Add($"{nameof(ClientSecret)} must not be blank.");
            }

            if (RedirectUris == null || RedirectUris.Count == 0)
            {
                problems.Add($"{nameof(RedirectUris)} must hold at least one redirect URI.");
            }
            else
            {
                foreach (string? redirectUri in RedirectUris)
                {
                    if (!OpenIdConnectAddress.TryParseBrowserFacing(redirectUri, out Uri? uri) || !string.IsNullOrEmpty(uri.Fragment))
                    {
                        problems.Add($"The redirect URI '{redirectUri}' must be an absolute https URL (or http on localhost, a name under it, or a loopback address) without a fragment.");
                    }
                }
            }

            if (!Enum.IsDefined(ClientAuthentication))
            {
                problems.Add($"{nameof(ClientAuthentication)} {(int)ClientAuthentication} is not a {nameof(OpenIdConnectClientAuthentication)} member.");
            }

            if (SigningAlgorithms == null || SigningAlgorithms.Count == 0)
            {
                problems.Add($"{nameof(SigningAlgorithms)} must hold at least one algorithm.");
            }
            else
            {
                foreach (string? algorithm in SigningAlgorithms)
                {
                    if (algorithm == null || !OpenIdConnectSigningAlgorithms.Asymmetric.Contains(algorithm))
                    {
                        problems.Add($"The signing algorithm '{algorithm}' is not one of the asymmetric algorithms allowed: {string.Join(", ", OpenIdConnectSigningAlgorithms.Asymmetric)}.");
                    }
                }
            }

            if (Scope == null || !Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("openid", StringComparer.Ordinal))
            {
                problems.Add($"{nameof(Scope)} '{Scope}' must hold the 'openid' scope.");
            }

            if (MetadataAddress != null && !OpenIdConnectAddress.TryParseFetchable(MetadataAddress, out Uri? _))
            {
                problems.Add($"{nameof(MetadataAddress)} '{MetadataAddress}' must be an absolute https URL (or http on a loopback host).");
            }

            if (isTemplate)
            {
                if (string.IsNullOrWhiteSpace(TenantClaim))
                {
                    problems.Add($"{nameof(TenantClaim)} must be null or non-blank.");
                }

                if (Issuer == null || !Issuer.Contains(TenantIdPlaceholder, StringComparison.Ordinal))
                {
                    problems.Add($"{nameof(Issuer)} must hold '{TenantIdPlaceholder}' when {nameof(TenantClaim)} is set.");
                }

                if (MetadataAddress == null)
                {
                    problems.Add($"{nameof(MetadataAddress)} must be set when {nameof(TenantClaim)} is set, since a template is no address.");
                }
            }

            return problems;
        }
    }
}
