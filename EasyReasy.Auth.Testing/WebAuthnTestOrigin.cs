namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// The origin a ceremony runs at, read once: serialized the way a browser writes it into the client
    /// data, with its host in the form a relying-party id is compared against.
    /// </summary>
    internal sealed class WebAuthnTestOrigin
    {
        /// <summary>
        /// The origin as a browser serializes it: <c>scheme://host[:port]</c>, lower-case, the host in
        /// punycode and the scheme's default port omitted.
        /// </summary>
        public string Serialized { get; }

        /// <summary>
        /// The host in punycode, lower-case: what a browser uses as the relying-party id when the options
        /// name none, and what an id the options do name must equal or be a parent domain of.
        /// </summary>
        public string Host { get; }

        private WebAuthnTestOrigin(string serialized, string host)
        {
            Serialized = serialized;
            Host = host;
        }

        /// <summary>
        /// Reads an origin the way a page's origin is: an http or https URL with a host and nothing after
        /// it.
        /// </summary>
        /// <param name="origin">The origin, such as <c>https://example.com</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="origin"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="origin"/> is not an http or https origin.</exception>
        public static WebAuthnTestOrigin Parse(string origin)
        {
            ArgumentNullException.ThrowIfNull(origin);

            if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || uri.IdnHost.Length == 0
                || uri.AbsolutePath != "/"
                || uri.Query.Length > 0
                || uri.Fragment.Length > 0
                || uri.UserInfo.Length > 0)
            {
                throw new ArgumentException(
                    $"The origin must be an http or https origin with no path, query, fragment or user info, such as https://example.com; got \"{origin}\".",
                    nameof(origin));
            }

            // IdnHost rather than Host: the browser serializes the punycode spelling, and the relying-party
            // id the server hashes is in punycode too, so the Unicode spelling would match neither.
            string host = uri.IdnHost.ToLowerInvariant();
            string authority = uri.IsDefaultPort ? host : $"{host}:{uri.Port}";

            return new WebAuthnTestOrigin($"{uri.Scheme}://{authority}", host);
        }

        /// <summary>
        /// Throws where a browser refuses a ceremony whose relying-party id is neither this origin's host
        /// nor a parent domain of it (WebAuthn Level 3 §5.1.3 step 8, §5.1.4.1 step 8).
        /// </summary>
        /// <param name="relyingPartyId">The relying-party id the options name.</param>
        /// <exception cref="InvalidOperationException">The id does not cover this origin's host.</exception>
        public void ThrowIfNotCoveredBy(string relyingPartyId)
        {
            bool covered = string.Equals(Host, relyingPartyId, StringComparison.OrdinalIgnoreCase)
                || Host.EndsWith("." + relyingPartyId, StringComparison.OrdinalIgnoreCase);

            if (!covered)
            {
                throw new InvalidOperationException(
                    $"The options' relying-party id \"{relyingPartyId}\" is neither the origin's host \"{Host}\" nor a parent domain of it, so a real browser refuses the ceremony.");
            }
        }
    }
}
