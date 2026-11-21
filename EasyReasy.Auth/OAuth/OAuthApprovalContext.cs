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
        /// The pending request's id, the one in the consent page's URL. Approving the same request again, after a double
        /// submit or a failed completion, gives the same id. Record the grant under it, and when one is already recorded
        /// for it, return that grant if the same person (<see cref="HttpContext"/>'s user) recorded it and refuse
        /// otherwise, so a repeated approval is idempotent and never hands one person's grant to another. The id stops
        /// being approvable once the request is completed or expires.
        /// </summary>
        public required string RequestId { get; init; }

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
