using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The relying-party identity a WebAuthn deployment is welded to: the RP ID that credentials are
    /// scoped to, the human-readable name authenticators show the user, and the set of origins whose
    /// pages may run a ceremony. Validated at construction, so a wrong value fails at startup rather
    /// than on the first registration.
    /// </summary>
    /// <remarks>
    /// A credential is bound to the RP ID it was created under and cannot be moved to another, so an RP ID
    /// that is wrong in production is not a configuration mistake that can be corrected afterwards — every
    /// credential registered under it has to be re-enrolled. That is why this type refuses to be constructed
    /// with a value that cannot be an RP ID at all, and why it collects every problem into one exception
    /// instead of reporting them one boot at a time.
    /// </remarks>
    public sealed class WebAuthnRelyingParty
    {
        /// <summary>
        /// The relying-party identifier: the registrable domain the credentials belong to, for example
        /// <c>example.com</c> or <c>localhost</c>. A bare domain — no scheme, no port, no path — lower-cased
        /// and in its ASCII (punycode) form, matching how a browser derives it from the page's effective domain.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// The human-readable relying-party name shown by the authenticator and by the browser's
        /// credential picker, for example <c>Contoso</c>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The origins whose pages may run a ceremony, in their serialized form (<c>scheme://host[:port]</c>,
        /// lower-cased, host in punycode, with the default port for the scheme omitted). A ceremony whose
        /// collected client data names an origin outside this set is rejected.
        /// </summary>
        /// <remarks>
        /// A set rather than a single value because one relying party legitimately serves several origins:
        /// sibling subdomains under the same RP ID (<c>https://app.example.com</c> and
        /// <c>https://admin.example.com</c> both under <c>example.com</c>), and the same host on a different
        /// scheme or port during development (<c>http://localhost:3000</c> alongside <c>https://localhost:5173</c>
        /// under the RP ID <c>localhost</c>). Every origin still has to sit under the RP ID — a browser
        /// refuses a ceremony whose page is not the RP ID or a subdomain of it — which is why an origin that
        /// does not is rejected here rather than at the first failed registration.
        /// </remarks>
        public IReadOnlySet<string> Origins { get; }

        /// <summary>
        /// SHA-256 of the UTF-8 encoding of <see cref="Id"/> — the value an authenticator puts in the first
        /// 32 bytes of its authenticator data. Computed once here so verification is a comparison, and
        /// handed out as <see cref="ReadOnlyMemory{T}"/> so the cached hash cannot be written through.
        /// </summary>
        internal ReadOnlyMemory<byte> IdHash { get; }

        /// <summary>
        /// Creates a validated relying-party configuration.
        /// </summary>
        /// <param name="id">The registrable domain credentials are scoped to, for example <c>example.com</c>. Case-insensitive; stored lower-cased and in punycode.</param>
        /// <param name="name">The human-readable name the authenticator shows the user. Surrounding whitespace is trimmed.</param>
        /// <param name="origins">
        /// The origins allowed to run a ceremony, for example <c>https://example.com</c>. Each must be an
        /// absolute URL carrying nothing but a scheme, host and optional port, and its host must be
        /// <paramref name="id"/> or a subdomain of it. The scheme must be <c>https</c>, or <c>http</c> for
        /// a loopback host — <c>localhost</c>, anything under it, or a loopback address — because WebAuthn
        /// runs only in a secure context and those are the origins a browser treats as one without TLS. At
        /// least one is required; duplicates that normalize to the same origin collapse.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="id"/>, <paramref name="name"/> or <paramref name="origins"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Any argument is invalid. The message lists every problem found, one per line, so a misconfigured
        /// deployment sees all of them at once rather than one per boot.
        /// </exception>
        public WebAuthnRelyingParty(string id, string name, IEnumerable<string> origins)
        {
            ArgumentNullException.ThrowIfNull(id);
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(origins);

            List<string> errors = new List<string>();

            string? normalizedId = NormalizeRelyingPartyId(id, errors);
            string normalizedName = name.Trim();

            if (normalizedName.Length == 0)
            {
                errors.Add($"'{nameof(name)}' must be a non-empty relying-party name.");
            }

            HashSet<string> normalizedOrigins = new HashSet<string>(StringComparer.Ordinal);
            int suppliedOriginCount = 0;
            foreach (string origin in origins)
            {
                suppliedOriginCount++;

                string? normalizedOrigin = NormalizeOrigin(origin, out string? host);
                if (normalizedOrigin == null || host == null)
                {
                    errors.Add($"'{nameof(origins)}' contains \"{origin}\", which is not an absolute http(s) origin of the form scheme://host[:port].");
                    continue;
                }

                // WebAuthn requires the RP ID to be the origin's effective domain or a registrable suffix of
                // it, so a browser answers any other pairing with a SecurityError. Checking it here turns a
                // deployment that can never complete a ceremony into a boot failure. Suffix matching against
                // the configured id needs no public-suffix list: "a.b.example.com" is under "example.com",
                // and an id that is itself a public suffix is the deployment's own mistake to make.
                if (normalizedId != null && host != normalizedId && !host.EndsWith($".{normalizedId}", StringComparison.Ordinal))
                {
                    errors.Add($"'{nameof(origins)}' contains \"{origin}\", whose host is neither \"{normalizedId}\" nor a subdomain of it; a browser refuses that pairing.");
                    continue;
                }

                normalizedOrigins.Add(normalizedOrigin);
            }

            if (suppliedOriginCount == 0)
            {
                errors.Add($"'{nameof(origins)}' must contain at least one allowed origin, for example \"https://example.com\".");
            }

            // The null check is part of the same condition rather than a separate guard: a null id always
            // added an error above, so it cannot reach here, and saying so in the condition is what lets the
            // assignments below be written without suppressing nullability.
            if (errors.Count > 0 || normalizedId == null)
            {
                // No paramName: the problems are aggregated across all three parameters, and naming one of
                // them would misattribute the rest.
                throw new ArgumentException($"Invalid WebAuthn relying-party configuration:\n{string.Join("\n", errors)}");
            }

            Id = normalizedId;
            Name = normalizedName;

            // Frozen rather than handed out as the live set it was built in: IsAllowedOrigin reads this
            // instance on every ceremony, so a caller who downcast the IReadOnlySet back to HashSet could
            // add an origin that never passed the validation above, permanently and for every request.
            Origins = normalizedOrigins.ToFrozenSet(StringComparer.Ordinal);
            IdHash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedId));
        }

        /// <summary>
        /// Whether the given origin, as a browser serialized it into collected client data, is allowed to
        /// run a ceremony for this relying party.
        /// </summary>
        /// <param name="origin">The origin string taken from the collected client data.</param>
        /// <returns><c>true</c> when the origin is one of the configured <see cref="Origins"/>.</returns>
        internal bool IsAllowedOrigin(string origin)
        {
            // Compared against the normalized form, not the raw configured string, so that a configured
            // "https://example.com:443/" and the browser's "https://example.com" are the same origin.
            string? normalizedOrigin = NormalizeOrigin(origin, out string? _);
            return normalizedOrigin != null && Origins.Contains(normalizedOrigin);
        }

        /// <summary>
        /// Reduces a relying-party id to the lower-cased ASCII domain a browser would derive, or returns
        /// null after recording why it cannot be one.
        /// </summary>
        private static string? NormalizeRelyingPartyId(string id, List<string> errors)
        {
            string trimmedId = id.Trim().ToLowerInvariant();

            if (trimmedId.Length == 0)
            {
                errors.Add($"'{nameof(id)}' must be a non-empty relying-party id (a registrable domain such as \"example.com\").");
                return null;
            }

            if (trimmedId.Contains('/', StringComparison.Ordinal)
                || trimmedId.Contains(':', StringComparison.Ordinal)
                || trimmedId.Any(char.IsWhiteSpace))
            {
                errors.Add($"'{nameof(id)}' must be a bare registrable domain with no scheme, port or path; got \"{id}\".");
                return null;
            }

            // A browser's RP ID is the ASCII form of the effective domain, so an internationalized domain
            // configured as Unicode would hash to something no authenticator ever reports.
            string? asciiId = ToAsciiDomain(trimmedId);
            if (asciiId == null)
            {
                errors.Add($"'{nameof(id)}' is not a domain name that can be encoded as ASCII; got \"{id}\".");
                return null;
            }

            return asciiId;
        }

        /// <summary>
        /// Reduces an origin to its serialized form — <c>scheme://host[:port]</c>, lower-cased, host in
        /// punycode, with the scheme's default port omitted — and reports the host separately. Returns null
        /// when the value cannot be an origin.
        /// </summary>
        private static string? NormalizeOrigin(string origin, out string? host)
        {
            host = null;

            if (string.IsNullOrWhiteSpace(origin))
            {
                return null;
            }

            if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out Uri? uri))
            {
                return null;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return null;
            }

            // WebAuthn only runs in a secure context, so a plain-http origin that is not loopback can never
            // complete a ceremony. Rejected here for the same reason the relying-party id's suffix rule is:
            // a deployment the browser will refuse should fail at construction rather than at its first
            // registration. Loopback is the exception the platform itself makes — a development origin over
            // http is a secure context, which is what lets a dev deployment work at all.
            if (uri.Scheme == Uri.UriSchemeHttp && !IsLoopbackOrigin(uri))
            {
                return null;
            }

            // An origin carries no path, query, fragment or userinfo. Rejecting rather than trimming them
            // keeps a configured "https://example.com/app" from silently becoming a broader origin than it
            // reads as, and keeps "https://evil.com@example.com" — which is an origin of example.com — from
            // being configured in a form that reads as evil.com.
            if (uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0)
            {
                return null;
            }

            // IdnHost is the punycode form, which is what a browser puts in the origin it serializes; Host
            // and Authority would keep the Unicode spelling and never match. IdnHost omits the port, so the
            // authority is recomposed here.
            host = uri.IdnHost.ToLowerInvariant();
            if (host.Length == 0)
            {
                return null;
            }

            string authority = uri.IsDefaultPort ? host : $"{host}:{uri.Port}";
            return $"{uri.Scheme}://{authority}";
        }

        /// <summary>
        /// Whether an origin's host is one the platform treats as a secure context without TLS.
        /// </summary>
        /// <remarks>
        /// The set the secure-contexts specification calls potentially trustworthy by virtue of being
        /// local: the loopback addresses, and the name <c>localhost</c> along with anything under it.
        /// <see cref="Uri.IsLoopback"/> covers 127.0.0.0/8, ::1 and <c>localhost</c> itself but not a
        /// subdomain of it, and browsers do treat <c>app.localhost</c> as trustworthy — so accepting only
        /// what that property reports would refuse a development origin the browser would have run, which
        /// is the same mistake as accepting one it would not.
        /// </remarks>
        private static bool IsLoopbackOrigin(Uri uri)
        {
            if (uri.IsLoopback)
            {
                return true;
            }

            string host = uri.IdnHost.ToLowerInvariant();
            return host.EndsWith(".localhost", StringComparison.Ordinal);
        }

        /// <summary>
        /// Converts a domain name to its ASCII (punycode) form, or returns null when it is not one.
        /// </summary>
        private static string? ToAsciiDomain(string domain)
        {
            try
            {
                return new IdnMapping().GetAscii(domain).ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
