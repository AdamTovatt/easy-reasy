using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The outcome of <see cref="OAuthClientAuthenticator.AuthenticateAsync"/>.
    /// </summary>
    internal sealed class OAuthClientAuthentication
    {
        private OAuthClientAuthentication(bool success, string? clientId, bool usedBasic, string? failure)
        {
            Success = success;
            ClientId = clientId;
            UsedBasic = usedBasic;
            Failure = failure;
        }

        /// <summary>
        /// Whether the client authenticated.
        /// </summary>
        [MemberNotNullWhen(true, nameof(ClientId))]
        [MemberNotNullWhen(false, nameof(Failure))]
        public bool Success { get; }

        /// <summary>
        /// The authenticated client id on success; on failure, the id the caller claimed, if any.
        /// </summary>
        public string? ClientId { get; }

        /// <summary>
        /// Whether the caller tried HTTP Basic, so a refusal answers with a Basic challenge (RFC 6749 section 5.2).
        /// </summary>
        public bool UsedBasic { get; }

        /// <summary>
        /// Why authentication failed.
        /// </summary>
        public string? Failure { get; }

        /// <summary>
        /// An authenticated client.
        /// </summary>
        /// <param name="clientId">The client id.</param>
        /// <returns>The outcome.</returns>
        public static OAuthClientAuthentication Succeeded(string clientId)
        {
            return new OAuthClientAuthentication(true, clientId, false, null);
        }

        /// <summary>
        /// A refused client.
        /// </summary>
        /// <param name="claimedClientId">The id the caller claimed, if any.</param>
        /// <param name="usedBasic">Whether the caller tried HTTP Basic.</param>
        /// <param name="failure">Why.</param>
        /// <returns>The outcome.</returns>
        public static OAuthClientAuthentication Failed(string? claimedClientId, bool usedBasic, string failure)
        {
            return new OAuthClientAuthentication(false, claimedClientId, usedBasic, failure);
        }

        /// <summary>
        /// The 401 <c>invalid_client</c> response, with a Basic challenge when the caller tried Basic.
        /// </summary>
        /// <param name="httpContext">The request.</param>
        /// <returns>The result.</returns>
        public IResult ToResult(HttpContext httpContext)
        {
            if (UsedBasic)
            {
                httpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"oauth\"";
            }

            return new OAuthError(OAuthError.InvalidClient, Failure ?? "Client authentication failed.").ToResult(StatusCodes.Status401Unauthorized);
        }
    }
}
