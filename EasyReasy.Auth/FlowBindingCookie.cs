using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The cookie that binds a browser-redirect sign-in flow (OpenID Connect, SAML) to the browser that began it. Begin
    /// sets a random value under a name carrying a new flow id and puts the id and the value's hash in the flow's state;
    /// completion refuses a state whose browser does not send the matching value. Someone who begins a sign-in and gets a
    /// victim's browser to finish it (login CSRF) is stopped here, because the victim's browser holds no cookie for the
    /// starter's flow.
    /// </summary>
    /// <remarks>
    /// The name starts with <c>__Host-</c>, which makes browsers insist on <c>Secure</c>, <c>Path=/</c> and no
    /// <c>Domain</c>, so a sibling subdomain cannot plant one. <c>SameSite=Lax</c> still sends it to the application's own
    /// page after the provider's top-level redirect, and with that page's same-site post of what the provider sent back;
    /// a provider's cross-site POST to the application (a <c>form_post</c> response) does not carry it, so a flow that
    /// uses this cookie has the provider redirect to a page of the application's own first.
    /// <para>
    /// One instance per kind of flow, each with its own name prefix, so each kind keeps its own limit of
    /// <see cref="MaxPendingFlows"/> and clearing one kind's cookies leaves the others alone.
    /// </para>
    /// </remarks>
    public sealed class FlowBindingCookie
    {
        /// <summary>
        /// The most binding cookies of one prefix a browser holds. A flow begun and never completed keeps its cookie for
        /// its lifetime, so without a limit repeated starts would grow the Cookie header until a proxy refuses the request
        /// or the browser evicts the site's other cookies. Enough for several sign-ins in progress in different tabs.
        /// </summary>
        public const int MaxPendingFlows = 5;

        private const string HostPrefix = "__Host-";
        private const int FlowIdSize = 16;

        /// <summary>
        /// Creates the binding cookie for one kind of flow.
        /// </summary>
        /// <param name="namePrefix">The name every cookie of this kind starts with, the flow id following: <c>__Host-</c>
        /// and then at least one letter, digit, <c>-</c>, <c>_</c> or <c>.</c>, for example
        /// <c>__Host-easyreasy-oidc-</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="namePrefix"/> is null.</exception>
        /// <exception cref="ArgumentException">The prefix does not have that shape.</exception>
        public FlowBindingCookie(string namePrefix)
        {
            ArgumentNullException.ThrowIfNull(namePrefix);

            if (!namePrefix.StartsWith(HostPrefix, StringComparison.Ordinal)
                || namePrefix.Length == HostPrefix.Length
                || !namePrefix[HostPrefix.Length..].All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
            {
                throw new ArgumentException(
                    $"The binding cookie name prefix '{namePrefix}' must be '{HostPrefix}' followed by at least one letter, digit, '-', '_' or '.'.",
                    nameof(namePrefix));
            }

            NamePrefix = namePrefix;
        }

        /// <summary>
        /// The name every cookie of this kind starts with.
        /// </summary>
        public string NamePrefix { get; }

        /// <summary>
        /// Sets a new binding cookie for a new flow on the response. When the request already carries
        /// <see cref="MaxPendingFlows"/> cookies of this kind, those are cleared first: they belong to flows the browser
        /// began and did not complete, whose completion then finds
        /// <see cref="FlowBindingCookieCheck.Missing"/>.
        /// </summary>
        /// <param name="httpContext">The request beginning the flow, whose response gets the cookie.</param>
        /// <param name="lifetime">How long the cookie lives: the flow's lifetime.</param>
        /// <returns>The flow's id and the cookie value's hash, for the flow's state.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="httpContext"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is not positive.</exception>
        public FlowBinding Issue(HttpContext httpContext, TimeSpan lifetime)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

            List<string> pending = httpContext.Request.Cookies.Keys.Where(name => name.StartsWith(NamePrefix, StringComparison.Ordinal)).ToList();
            if (pending.Count >= MaxPendingFlows)
            {
                // The cookies carry no age to keep the newest by, and a browser this far over is abandoning flows.
                foreach (string name in pending)
                {
                    httpContext.Response.Cookies.Delete(name, CreateOptions(lifetime: null));
                }
            }

            string flowId = Base64UrlEncoding.Encode(RandomNumberGenerator.GetBytes(FlowIdSize));
            string value = SecureToken.Generate();
            httpContext.Response.Cookies.Append(GetName(flowId), value, CreateOptions(lifetime));
            return new FlowBinding(flowId, SecureToken.Hash(value));
        }

        /// <summary>
        /// Checks the request's binding cookie for a flow against the hash in its state.
        /// </summary>
        /// <param name="httpContext">The request completing the flow.</param>
        /// <param name="binding">The flow's id and hash, from its state.</param>
        /// <returns>Whether the cookie matches, is missing, or holds another value.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public FlowBindingCookieCheck Check(HttpContext httpContext, FlowBinding binding)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            ArgumentNullException.ThrowIfNull(binding);

            // ASP.NET Core's request cookie parser drops a cookie with an empty value, so one is missing here too.
            if (!httpContext.Request.Cookies.TryGetValue(GetName(binding.FlowId), out string? value))
            {
                return FlowBindingCookieCheck.Missing;
            }

            return SecureToken.MatchesHash(value, binding.ValueHash) ? FlowBindingCookieCheck.Matches : FlowBindingCookieCheck.Mismatch;
        }

        /// <summary>
        /// Expires a flow's binding cookie on the response. Clear it whenever a flow reaches completion, whatever the
        /// outcome, so a state cannot be completed twice in the same browser.
        /// </summary>
        /// <param name="httpContext">The request completing the flow, whose response clears the cookie.</param>
        /// <param name="binding">The flow's id and hash, from its state.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public void Clear(HttpContext httpContext, FlowBinding binding)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            ArgumentNullException.ThrowIfNull(binding);

            httpContext.Response.Cookies.Delete(GetName(binding.FlowId), CreateOptions(lifetime: null));
        }

        private string GetName(string flowId) => NamePrefix + flowId;

        private static CookieOptions CreateOptions(TimeSpan? lifetime)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = lifetime,
                IsEssential = true,
            };
        }
    }
}
