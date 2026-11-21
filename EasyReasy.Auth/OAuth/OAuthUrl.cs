using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The URL rules the authorization server applies to its own configuration and to the URLs clients send.
    /// </summary>
    internal static class OAuthUrl
    {
        /// <summary>
        /// Parses an absolute URL a browser opens as a secure context (<see cref="SecureContextUrl.IsSecureContext"/>):
        /// the rule for the server's configured URLs and a client's home page. A redirect URI follows RFC 8252 instead,
        /// in <see cref="OAuthRedirectUri"/>.
        /// </summary>
        /// <param name="address">The address to parse.</param>
        /// <param name="uri">The parsed URL when it is allowed.</param>
        /// <returns>Whether the address is allowed.</returns>
        public static bool TryParseSecureContextUrl(string? address, [NotNullWhen(true)] out Uri? uri)
        {
            if (address != null
                && Uri.TryCreate(address, UriKind.Absolute, out Uri? parsed)
                && SecureContextUrl.IsSecureContext(parsed))
            {
                uri = parsed;
                return true;
            }

            uri = null;
            return false;
        }

        /// <summary>
        /// The origin of an absolute URL (scheme, host and port), lowercased, with no trailing slash.
        /// </summary>
        /// <param name="uri">An absolute URL.</param>
        /// <returns>The origin.</returns>
        public static string Origin(Uri uri)
        {
            return uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
        }

        /// <summary>
        /// The path of an absolute URL without a trailing slash: empty for the root.
        /// </summary>
        /// <param name="uri">An absolute URL.</param>
        /// <returns>The path.</returns>
        public static string PathWithoutTrailingSlash(Uri uri)
        {
            return uri.AbsolutePath.TrimEnd('/');
        }

        /// <summary>
        /// Whether a URL has a fragment, including an empty one (<c>https://a/b#</c>), which <see cref="Uri.Fragment"/>
        /// reports as empty.
        /// </summary>
        /// <param name="address">The original address.</param>
        /// <returns>Whether it carries a fragment.</returns>
        public static bool HasFragment(string address)
        {
            return address.Contains('#');
        }

        /// <summary>
        /// Appends query parameters to a URL that may already have a query, encoding each name and value.
        /// </summary>
        /// <param name="baseUrl">The URL to append to.</param>
        /// <param name="parameters">The parameters, in order; a null value is left out.</param>
        /// <returns>The URL with the parameters appended.</returns>
        public static string AppendQuery(string baseUrl, IEnumerable<KeyValuePair<string, string?>> parameters)
        {
            StringBuilder builder = new StringBuilder(baseUrl);
            bool hasQuery = baseUrl.Contains('?');
            foreach (KeyValuePair<string, string?> parameter in parameters)
            {
                if (parameter.Value == null)
                {
                    continue;
                }

                builder.Append(hasQuery ? '&' : '?');
                hasQuery = true;
                builder.Append(Uri.EscapeDataString(parameter.Key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(parameter.Value));
            }

            return builder.ToString();
        }
    }
}
