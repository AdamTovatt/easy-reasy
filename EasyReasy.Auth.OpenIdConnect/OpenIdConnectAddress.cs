using System.Diagnostics.CodeAnalysis;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The rules for an address this package uses, all absolute and https, differing only in which plain-http hosts they
    /// allow: the server's own fetches stay on loopback, an address a provider's document names may use it only for a
    /// local provider, and an address the browser goes to follows the browser's secure-context rule.
    /// </summary>
    internal static class OpenIdConnectAddress
    {
        /// <summary>
        /// Whether the server may fetch from an address the application configured (an issuer, a discovery document):
        /// https, or http on a loopback address or <c>localhost</c>, which resolve to this machine alone.
        /// </summary>
        /// <param name="address">The address to check.</param>
        /// <param name="uri">The parsed address, when it is allowed.</param>
        /// <returns><c>true</c> when the address is allowed.</returns>
        public static bool TryParseFetchable(string? address, [NotNullWhen(true)] out Uri? uri)
        {
            return TryParse(address, parsed => parsed.IsLoopback, out uri);
        }

        /// <summary>
        /// Whether the server may fetch from or post to an address a provider's discovery document named (a key set, a
        /// token endpoint): https, and http on a loopback host only for a provider whose own discovery address is such
        /// a local one. A remote provider's document cannot point the server at this machine's own plain-http services.
        /// </summary>
        /// <param name="address">The address the document named.</param>
        /// <param name="metadataAddress">The provider's discovery address, which the application configured.</param>
        /// <param name="uri">The parsed address, when it is allowed.</param>
        /// <returns><c>true</c> when the address is allowed.</returns>
        public static bool TryParseFetchableFrom(string? address, string metadataAddress, [NotNullWhen(true)] out Uri? uri)
        {
            bool providerIsLocal = TryParseFetchable(metadataAddress, out Uri? metadata) && metadata.Scheme == Uri.UriSchemeHttp;
            return TryParse(address, parsed => providerIsLocal && parsed.IsLoopback, out uri);
        }

        /// <summary>
        /// Whether the browser may be sent to an address (a redirect URI, an authorize endpoint): https, or http on a host
        /// the browser treats as a secure context without TLS, the loopback addresses, <c>localhost</c> and any name under
        /// it such as <c>app.localhost</c>. The binding cookie is <c>Secure</c>, so the browser must hold the page to be
        /// secure.
        /// </summary>
        /// <param name="address">The address to check.</param>
        /// <param name="uri">The parsed address, when it is allowed.</param>
        /// <returns><c>true</c> when the address is allowed.</returns>
        public static bool TryParseBrowserFacing(string? address, [NotNullWhen(true)] out Uri? uri)
        {
            return TryParse(address, parsed => parsed.IsLoopback || parsed.IdnHost.ToLowerInvariant().EndsWith(".localhost", StringComparison.Ordinal), out uri);
        }

        private static bool TryParse(string? address, Func<Uri, bool> allowsPlainHttp, [NotNullWhen(true)] out Uri? uri)
        {
            if (address != null
                && Uri.TryCreate(address, UriKind.Absolute, out Uri? parsed)
                && (parsed.Scheme == Uri.UriSchemeHttps || (parsed.Scheme == Uri.UriSchemeHttp && allowsPlainHttp(parsed))))
            {
                uri = parsed;
                return true;
            }

            uri = null;
            return false;
        }
    }
}
