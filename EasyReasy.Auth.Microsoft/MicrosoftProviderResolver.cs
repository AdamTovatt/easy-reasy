using EasyReasy.Auth.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The app registration as an OpenID Connect provider per tenant segment: <c>organizations</c> for any work or school
    /// tenant, or one tenant's id. The provider key is the segment, so a flow's state names the tenant it was begun for.
    /// </summary>
    internal sealed class MicrosoftProviderResolver : IOpenIdConnectProviderResolver
    {
        private const OpenIdConnectClientAuthentication ClientAuthentication = OpenIdConnectClientAuthentication.ClientSecretPost;

        /// <summary>
        /// Entra signs ID tokens with RS256 only.
        /// </summary>
        private static readonly IReadOnlyList<string> SigningAlgorithms = [SecurityAlgorithms.RsaSha256];

        private readonly MicrosoftAuthority _authority;
        private readonly OpenIdConnectProvider _organizationsProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="MicrosoftProviderResolver"/> class, building the
        /// <c>organizations</c> provider now. Check the values with <see cref="FindProblems"/> first.
        /// </summary>
        /// <param name="authority">The authority built from the options.</param>
        /// <param name="clientId">The app registration's client id.</param>
        /// <param name="clientSecret">The app registration's client secret.</param>
        /// <param name="redirectUris">The redirect URIs, copied now.</param>
        /// <exception cref="ArgumentException">A value breaks a rule <see cref="FindProblems"/> reports.</exception>
        public MicrosoftProviderResolver(MicrosoftAuthority authority, string clientId, string clientSecret, IReadOnlyList<string> redirectUris)
        {
            _authority = authority;
            _organizationsProvider = CreateProvider(authority.OrganizationsIssuer, clientId, clientSecret, redirectUris);
        }

        /// <summary>
        /// Every rule the app registration's values break as a provider, one line each.
        /// </summary>
        /// <param name="clientId">The client id.</param>
        /// <param name="clientSecret">The client secret.</param>
        /// <param name="redirectUris">The redirect URIs.</param>
        /// <returns>The problems, empty when there are none.</returns>
        public static IReadOnlyList<string> FindProblems(string clientId, string clientSecret, IReadOnlyList<string> redirectUris)
        {
            return OpenIdConnectProvider.FindProblems(clientId, clientSecret, redirectUris, ClientAuthentication, SigningAlgorithms, MicrosoftAuthority.Scope);
        }

        /// <inheritdoc />
        public Task<OpenIdConnectProvider?> FindProviderAsync(string providerKey, CancellationToken cancellationToken)
        {
            return Task.FromResult(Find(providerKey));
        }

        /// <summary>
        /// The provider for a tenant segment, or null for a key that is no segment.
        /// </summary>
        /// <param name="segment">The tenant segment.</param>
        /// <returns>The provider, or null.</returns>
        public OpenIdConnectProvider? Find(string segment)
        {
            if (segment == MicrosoftAuthority.OrganizationsSegment)
            {
                return _organizationsProvider;
            }

            OpenIdConnectIssuer? issuer = _authority.FindIssuer(segment);
            if (issuer == null)
            {
                return null;
            }

            // The same client as the organizations provider, already checked when that was built.
            return CreateProvider(issuer, _organizationsProvider.ClientId, _organizationsProvider.ClientSecret, _organizationsProvider.RedirectUris);
        }

        private static OpenIdConnectProvider CreateProvider(OpenIdConnectIssuer issuer, string clientId, string clientSecret, IReadOnlyList<string> redirectUris)
        {
            return new OpenIdConnectProvider(issuer, clientId, clientSecret, redirectUris, ClientAuthentication, SigningAlgorithms, MicrosoftAuthority.Scope);
        }
    }
}
