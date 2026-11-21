using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// What <see cref="IOAuthGrantHandler.ApproveAsync"/> is deciding on: a signed-in person approving a pending
    /// authorization request on the application's consent page.
    /// </summary>
    public sealed class OAuthApprovalContext
    {
        /// <summary>
        /// The approve request. Its <see cref="HttpContext.User"/> is the person signed in with the application's own
        /// token; the endpoint requires one, and the handler decides whether that sign-in is enough (a completed second
        /// factor, say).
        /// </summary>
        public required HttpContext HttpContext { get; init; }

        /// <summary>
        /// The client asking to connect.
        /// </summary>
        public required OAuthClientInfo Client { get; init; }

        /// <summary>
        /// The redirect URI the code will be sent to.
        /// </summary>
        public required string RedirectUri { get; init; }

        /// <summary>
        /// The scopes the client requested, in order, empty when it requested none.
        /// </summary>
        public required IReadOnlyList<string> RequestedScopes { get; init; }

        /// <summary>
        /// The resource the client named, or null when it named none.
        /// </summary>
        public string? Resource { get; init; }

        /// <summary>
        /// The approve request's JSON body, which the application defines (in Ordo: the organization, and for a system
        /// its grants), or null when the request had no body. The library does not read it; validate it as untrusted
        /// input.
        /// </summary>
        public JsonElement? ApprovalBody { get; init; }
    }
}
