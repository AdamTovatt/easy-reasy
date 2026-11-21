using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth
{
    /// <summary>
    /// An OAuth error: its code from the RFC that defines it, and a description for the developer reading the response.
    /// </summary>
    internal sealed class OAuthError
    {
        /// <summary>RFC 6749: the request is missing a parameter, repeats one, or is otherwise malformed.</summary>
        public const string InvalidRequest = "invalid_request";

        /// <summary>RFC 6749: client authentication failed, or the client is unknown.</summary>
        public const string InvalidClient = "invalid_client";

        /// <summary>RFC 6749: the code or refresh token is invalid, expired, revoked, used, or issued to another client or redirect URI.</summary>
        public const string InvalidGrant = "invalid_grant";

        /// <summary>RFC 6749: the grant type is not supported.</summary>
        public const string UnsupportedGrantType = "unsupported_grant_type";

        /// <summary>RFC 6749: the response type is not supported.</summary>
        public const string UnsupportedResponseType = "unsupported_response_type";

        /// <summary>RFC 6749: the person or the server denied the request.</summary>
        public const string AccessDenied = "access_denied";

        /// <summary>RFC 8707: the resource is not this server's.</summary>
        public const string InvalidTarget = "invalid_target";

        /// <summary>RFC 7591: a redirect URI is not acceptable.</summary>
        public const string InvalidRedirectUri = "invalid_redirect_uri";

        /// <summary>RFC 7591: another metadata value is not acceptable.</summary>
        public const string InvalidClientMetadata = "invalid_client_metadata";

        /// <summary>
        /// Initializes an error.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <param name="description">The description.</param>
        public OAuthError(string code, string description)
        {
            Code = code;
            Description = description;
        }

        /// <summary>
        /// The error code.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// The description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// The JSON error response (RFC 6749 section 5.2).
        /// </summary>
        /// <param name="statusCode">The HTTP status.</param>
        /// <returns>The result.</returns>
        public IResult ToResult(int statusCode = StatusCodes.Status400BadRequest)
        {
            Dictionary<string, string> body = new Dictionary<string, string>
            {
                ["error"] = Code,
                ["error_description"] = Description,
            };
            return Results.Json(body, OAuthJson.Options, statusCode: statusCode);
        }
    }
}
