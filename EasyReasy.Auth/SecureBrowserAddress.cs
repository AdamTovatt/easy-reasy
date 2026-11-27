using System.Diagnostics.CodeAnalysis;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The rule for an address a sign-in sends the browser to, or has it post to (a redirect URI, an authorization or
    /// single sign-on endpoint, an assertion consumer service): absolute and https, or http on a host the browser treats
    /// as a secure context without TLS, the loopback addresses, <c>localhost</c> and any name under it such as
    /// <c>app.localhost</c>. Any other scheme, such as <c>javascript:</c>, is refused, and the
    /// <see cref="FlowBindingCookie"/> is <c>Secure</c>, so the page the browser holds must be secure too.
    /// </summary>
    public static class SecureBrowserAddress
    {
        /// <summary>
        /// Whether the browser may be sent to an address.
        /// </summary>
        /// <param name="address">The address to check.</param>
        /// <param name="uri">The parsed address, when it is allowed.</param>
        /// <returns><c>true</c> when the address is allowed.</returns>
        public static bool TryParse(string? address, [NotNullWhen(true)] out Uri? uri)
        {
            if (address != null
                && Uri.TryCreate(address, UriKind.Absolute, out Uri? parsed)
                && (parsed.Scheme == Uri.UriSchemeHttps || (parsed.Scheme == Uri.UriSchemeHttp && IsSecureContextHost(parsed))))
            {
                uri = parsed;
                return true;
            }

            uri = null;
            return false;
        }

        /// <summary>
        /// Whether the browser may be sent to an address.
        /// </summary>
        /// <param name="address">The address to check.</param>
        /// <returns><c>true</c> when the address is allowed.</returns>
        public static bool IsAllowed(Uri? address)
        {
            return address != null && address.IsAbsoluteUri && TryParse(address.OriginalString, out Uri? _);
        }

        private static bool IsSecureContextHost(Uri address)
        {
            return address.IsLoopback || address.IdnHost.ToLowerInvariant().EndsWith(".localhost", StringComparison.Ordinal);
        }
    }
}
