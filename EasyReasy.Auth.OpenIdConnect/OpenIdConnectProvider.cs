using Microsoft.IdentityModel.Tokens;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// One OpenID Connect provider the application signs people in with: its issuer and the client registered there.
    /// The authorize and token endpoints and the signing keys come from the issuer's discovery document, whose issuer
    /// must equal <see cref="OpenIdConnectIssuer.Value"/>.
    /// </summary>
    /// <remarks>
    /// Returned by the application's <see cref="IOpenIdConnectProviderResolver"/> per request, so a provider can be added
    /// or changed without a restart. Checked when built: the constructor throws for a provider breaking a rule, so a
    /// resolver can only return a usable one.
    /// </remarks>
    public sealed class OpenIdConnectProvider
    {
        /// <summary>
        /// The default <see cref="Scope"/>: an ID token, with the profile and email claims.
        /// </summary>
        public const string DefaultScope = "openid profile email";

        /// <summary>
        /// Builds a provider, checking every value.
        /// </summary>
        /// <param name="issuer">The issuer, usually <see cref="OpenIdConnectIssuer.Fixed"/>.</param>
        /// <param name="clientId">The client id registered at the provider.</param>
        /// <param name="clientSecret">The client secret.</param>
        /// <param name="redirectUris">The redirect URIs a flow with this provider may name (see
        /// <see cref="RedirectUris"/>); at least one, each absolute https (http only on <c>localhost</c>, a name under it,
        /// or a loopback address) without a fragment.</param>
        /// <param name="clientAuthentication">How the client secret is sent to the token endpoint.</param>
        /// <param name="signingAlgorithms">The algorithms an ID token may be signed with (see
        /// <see cref="SigningAlgorithms"/>), or null for RS256 alone.</param>
        /// <param name="scope">The space-separated scopes a flow asks for, holding <c>openid</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="issuer"/> is null.</exception>
        /// <exception cref="ArgumentException">A value breaks its rule, every problem reported at once.</exception>
        public OpenIdConnectProvider(
            OpenIdConnectIssuer issuer,
            string clientId,
            string clientSecret,
            IReadOnlyList<string> redirectUris,
            OpenIdConnectClientAuthentication clientAuthentication = OpenIdConnectClientAuthentication.ClientSecretBasic,
            IReadOnlyList<string>? signingAlgorithms = null,
            string scope = DefaultScope)
        {
            ArgumentNullException.ThrowIfNull(issuer);

            // Copied, so a caller changing its list afterwards cannot change a checked provider.
            IReadOnlyList<string> redirectUriCopy = redirectUris?.ToList() ?? [];
            IReadOnlyList<string> signingAlgorithmCopy = signingAlgorithms?.ToList() ?? [SecurityAlgorithms.RsaSha256];

            IReadOnlyList<string> problems = FindProblems(clientId, clientSecret, redirectUriCopy, clientAuthentication, signingAlgorithmCopy, scope);
            if (problems.Count > 0)
            {
                throw new ArgumentException($"Invalid {nameof(OpenIdConnectProvider)}:\n{string.Join("\n", problems)}");
            }

            Issuer = issuer;
            ClientId = clientId;
            ClientSecret = clientSecret;
            RedirectUris = redirectUriCopy;
            ClientAuthentication = clientAuthentication;
            SigningAlgorithms = signingAlgorithmCopy;
            Scope = scope;
        }

        /// <summary>
        /// The issuer, and where its discovery document is read from.
        /// </summary>
        public OpenIdConnectIssuer Issuer { get; }

        /// <summary>
        /// The client id registered at the provider. An ID token's audience must hold it.
        /// </summary>
        public string ClientId { get; }

        /// <summary>
        /// The client secret, sent to the token endpoint as <see cref="ClientAuthentication"/> says. It never reaches the
        /// browser.
        /// </summary>
        public string ClientSecret { get; }

        /// <summary>
        /// The redirect URIs a flow with this provider may name, each registered at the provider. Give every provider
        /// redirect URIs of its own, never shared with another provider: completion refuses a response that arrived at
        /// another redirect URI than the flow named, which is what stops a provider from passing off another's
        /// response (a mix-up), and that only holds while no two providers share one. Matching is exact.
        /// </summary>
        public IReadOnlyList<string> RedirectUris { get; }

        /// <summary>
        /// How the client secret is sent to the token endpoint. Defaults to
        /// <see cref="OpenIdConnectClientAuthentication.ClientSecretBasic"/>.
        /// </summary>
        public OpenIdConnectClientAuthentication ClientAuthentication { get; }

        /// <summary>
        /// The algorithms an ID token may be signed with, each asymmetric (RS, PS or ES at 256, 384 or 512). Defaults to
        /// RS256 alone, which every provider supports. A token signed otherwise is refused.
        /// </summary>
        public IReadOnlyList<string> SigningAlgorithms { get; }

        /// <summary>
        /// The space-separated scopes a flow asks for. Holds <c>openid</c>. Defaults to <see cref="DefaultScope"/>.
        /// </summary>
        public string Scope { get; }

        /// <summary>
        /// Every rule the client's values break, one line each, which the constructor throws for. A package that builds
        /// providers from its own options reports these with its other problems at startup.
        /// </summary>
        /// <param name="clientId">The client id.</param>
        /// <param name="clientSecret">The client secret.</param>
        /// <param name="redirectUris">The redirect URIs.</param>
        /// <param name="clientAuthentication">How the client secret is sent.</param>
        /// <param name="signingAlgorithms">The signing algorithms, or null for RS256 alone.</param>
        /// <param name="scope">The scopes.</param>
        /// <returns>The problems, empty when there are none.</returns>
        public static IReadOnlyList<string> FindProblems(
            string clientId,
            string clientSecret,
            IReadOnlyList<string> redirectUris,
            OpenIdConnectClientAuthentication clientAuthentication = OpenIdConnectClientAuthentication.ClientSecretBasic,
            IReadOnlyList<string>? signingAlgorithms = null,
            string scope = DefaultScope)
        {
            redirectUris ??= [];
            signingAlgorithms ??= [SecurityAlgorithms.RsaSha256];
            List<string> problems = new List<string>();

            if (string.IsNullOrWhiteSpace(clientId))
            {
                problems.Add("The client id must not be blank.");
            }

            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                problems.Add("The client secret must not be blank.");
            }

            if (redirectUris.Count == 0)
            {
                problems.Add("There must be at least one redirect URI.");
            }

            foreach (string? redirectUri in redirectUris)
            {
                // Completion compares the address the page posts, its origin plus path, exactly; the browser lowercases
                // the host and drops a default port, query and fragment, so any other form could never match.
                if (!OpenIdConnectAddress.TryParseBrowserFacing(redirectUri, out Uri? uri)
                    || !string.IsNullOrEmpty(uri.UserInfo)
                    || !string.Equals(uri.GetLeftPart(UriPartial.Path), redirectUri, StringComparison.Ordinal))
                {
                    problems.Add($"The redirect URI '{redirectUri}' must be an absolute https URL (or http on localhost, a name under it, or a loopback address) written as the page's origin plus path: a lowercase host, no default port, user info, query or fragment.");
                }
            }

            if (!Enum.IsDefined(clientAuthentication))
            {
                problems.Add($"The client authentication {(int)clientAuthentication} is not a {nameof(OpenIdConnectClientAuthentication)} member.");
            }

            if (signingAlgorithms.Count == 0)
            {
                problems.Add("There must be at least one signing algorithm.");
            }

            foreach (string? algorithm in signingAlgorithms)
            {
                if (algorithm == null || !OpenIdConnectSigningAlgorithms.Asymmetric.Contains(algorithm))
                {
                    problems.Add($"The signing algorithm '{algorithm}' is not one of the asymmetric algorithms allowed: {string.Join(", ", OpenIdConnectSigningAlgorithms.Asymmetric)}.");
                }
            }

            if (scope == null || !scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("openid", StringComparer.Ordinal))
            {
                problems.Add($"The scope '{scope}' must hold the 'openid' scope.");
            }

            return problems;
        }
    }
}
