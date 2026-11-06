using Microsoft.AspNetCore.Http;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The cookie that binds a flow to the browser that began it. Begin sets a random value under a name carrying
    /// the flow's id and puts the value's hash in the state; Complete refuses a state whose browser does not send the
    /// matching value. Someone who begins a sign-in and gets a victim's browser to finish it (login CSRF) is stopped
    /// here, because the victim's browser holds no cookie for the starter's flow.
    /// </summary>
    /// <remarks>
    /// <c>__Host-</c> makes browsers insist on <c>Secure</c>, <c>Path=/</c> and no <c>Domain</c>, so a sibling
    /// subdomain cannot plant one. <c>SameSite=Lax</c> still sends it to the application's own page after
    /// Microsoft's top-level redirect, and with that page's post of the code and state.
    /// </remarks>
    internal static class MicrosoftBindingCookie
    {
        /// <summary>
        /// The name every binding cookie starts with; the flow id follows.
        /// </summary>
        public const string NamePrefix = "__Host-easyreasy-microsoft-";

        private const int ValueSize = 32;

        /// <summary>
        /// Sets a new binding cookie for a flow on the response.
        /// </summary>
        /// <param name="httpContext">The context whose response gets the cookie.</param>
        /// <param name="flowId">The flow's id.</param>
        /// <param name="lifetime">How long the cookie lives: the state's lifetime.</param>
        /// <returns>The hash of the cookie's value, for the state.</returns>
        public static string Issue(HttpContext httpContext, string flowId, TimeSpan lifetime)
        {
            string value = MicrosoftRandomValue.Create(ValueSize);
            httpContext.Response.Cookies.Append(GetName(flowId), value, CreateOptions(lifetime));
            return Hash(value);
        }

        /// <summary>
        /// Checks the request's binding cookie for a flow against the hash in its state.
        /// </summary>
        /// <param name="httpContext">The context whose request is checked.</param>
        /// <param name="flowId">The flow's id.</param>
        /// <param name="expectedHash">The hash from the state.</param>
        /// <returns>Null when the cookie matches, otherwise the reason it does not.</returns>
        public static MicrosoftAuthFailureReason? Check(HttpContext httpContext, string flowId, string expectedHash)
        {
            if (!httpContext.Request.Cookies.TryGetValue(GetName(flowId), out string? value) || string.IsNullOrEmpty(value))
            {
                return MicrosoftAuthFailureReason.BindingCookieMissing;
            }

            bool matches = CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(Hash(value)),
                Encoding.ASCII.GetBytes(expectedHash));

            return matches ? null : MicrosoftAuthFailureReason.BindingCookieMismatch;
        }

        /// <summary>
        /// Expires a flow's binding cookie on the response.
        /// </summary>
        /// <param name="httpContext">The context whose response clears the cookie.</param>
        /// <param name="flowId">The flow's id.</param>
        public static void Clear(HttpContext httpContext, string flowId)
        {
            httpContext.Response.Cookies.Delete(GetName(flowId), CreateOptions(lifetime: null));
        }

        private static string GetName(string flowId) => NamePrefix + flowId;

        private static string Hash(string value)
        {
            return Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        }

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
