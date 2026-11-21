namespace EasyReasy.Auth
{
    /// <summary>
    /// The rules for a client's redirect URIs: which ones a client may register, and when a request's redirect URI
    /// matches a registered one.
    /// </summary>
    internal static class OAuthRedirectUri
    {
        /// <summary>
        /// The longest redirect URI accepted.
        /// </summary>
        public const int MaxLength = 2000;

        /// <summary>
        /// Schemes that run or show content in the browser itself, or reach somewhere other than an app, and so can never
        /// be a redirect URI's.
        /// </summary>
        private static readonly HashSet<string> RefusedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "javascript", "vbscript", "data", "file", "about", "blob", "filesystem", "view-source", "ftp", "ws", "wss", "mailto", "tel", "sms",
        };

        /// <summary>
        /// Whether a client may register a redirect URI: an absolute https URL, an http URL on a loopback host (a native
        /// app's local listener, RFC 8252 section 7.3), or a desktop app's private-use scheme such as
        /// <c>com.example.app:/callback</c> or <c>cursor://...</c> (RFC 8252 section 7.1; PKCE is what keeps another app
        /// claiming the scheme from redeeming the code). Never a scheme the browser handles itself (<c>javascript:</c>,
        /// <c>data:</c>, <c>file:</c> and the like), never with a fragment (RFC 6749 section 3.1.2), never with userinfo.
        /// </summary>
        /// <param name="redirectUri">The redirect URI.</param>
        /// <returns>Whether it may be registered.</returns>
        public static bool IsAllowed(string? redirectUri)
        {
            if (string.IsNullOrEmpty(redirectUri)
                || redirectUri.Length > MaxLength
                || OAuthUrl.HasFragment(redirectUri)
                || !Uri.TryCreate(redirectUri, UriKind.Absolute, out Uri? uri)
                || !string.IsNullOrEmpty(uri.UserInfo))
            {
                return false;
            }

            if (uri.Scheme == Uri.UriSchemeHttps)
            {
                return true;
            }

            if (uri.Scheme == Uri.UriSchemeHttp)
            {
                return uri.IsLoopback;
            }

            return !RefusedSchemes.Contains(uri.Scheme);
        }

        /// <summary>
        /// Whether a request's redirect URI matches a registered one: by exact string comparison, except that for a
        /// registered http loopback URI the port may differ, since a native app's listener gets whatever port is free
        /// (RFC 8252 section 7.3).
        /// </summary>
        /// <param name="registered">A registered redirect URI.</param>
        /// <param name="requested">The request's redirect URI.</param>
        /// <returns>Whether they match.</returns>
        public static bool Matches(string registered, string requested)
        {
            if (string.Equals(registered, requested, StringComparison.Ordinal))
            {
                return true;
            }

            if (!Uri.TryCreate(registered, UriKind.Absolute, out Uri? registeredUri)
                || registeredUri.Scheme != Uri.UriSchemeHttp
                || !registeredUri.IsLoopback
                || OAuthUrl.HasFragment(requested)
                || !Uri.TryCreate(requested, UriKind.Absolute, out Uri? requestedUri)
                || requestedUri.Scheme != Uri.UriSchemeHttp
                || !string.IsNullOrEmpty(requestedUri.UserInfo))
            {
                return false;
            }

            // Everything but the port. Both sides are parsed the same way, and the host is loopback, so the parse's
            // normalising cannot send the code anywhere but the machine the client runs on.
            return string.Equals(WithoutPort(registeredUri), WithoutPort(requestedUri), StringComparison.Ordinal);
        }

        private static string WithoutPort(Uri uri)
        {
            return uri.Scheme + "://" + uri.Host + uri.PathAndQuery;
        }
    }
}
