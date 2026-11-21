namespace EasyReasy.Auth
{
    /// <summary>
    /// The browser's secure-context rule for a URL the library is configured with: https, or http on a host the platform
    /// treats as local. WebAuthn origins and the OAuth server's browser-facing URLs both follow it.
    /// </summary>
    internal static class SecureContextUrl
    {
        /// <summary>
        /// Whether a URL is https, or http on a local host (<see cref="IsLocalHost"/>).
        /// </summary>
        /// <param name="uri">An absolute URL.</param>
        /// <returns>Whether a browser treats it as a secure context.</returns>
        public static bool IsSecureContext(Uri uri)
        {
            return uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && IsLocalHost(uri));
        }

        /// <summary>
        /// Whether a URL's host is one the platform treats as a secure context without TLS.
        /// </summary>
        /// <remarks>
        /// The set the secure-contexts specification calls potentially trustworthy by virtue of being
        /// local: the loopback addresses, and the name <c>localhost</c> along with anything under it.
        /// <see cref="Uri.IsLoopback"/> covers 127.0.0.0/8, ::1 and <c>localhost</c> itself but not a
        /// subdomain of it, and browsers do treat <c>app.localhost</c> as trustworthy — so accepting only
        /// what that property reports would refuse a development origin the browser would have run, which
        /// is the same mistake as accepting one it would not.
        /// </remarks>
        /// <param name="uri">An absolute URL.</param>
        /// <returns>Whether its host is local.</returns>
        public static bool IsLocalHost(Uri uri)
        {
            if (uri.IsLoopback)
            {
                return true;
            }

            string host = uri.IdnHost.ToLowerInvariant();
            return host.EndsWith(".localhost", StringComparison.Ordinal);
        }
    }
}
