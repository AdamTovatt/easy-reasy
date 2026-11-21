using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Dynamic client registration (RFC 7591): records a client, grants it nothing.
    /// </summary>
    internal sealed class OAuthRegistrationEndpoint
    {
        private readonly IOAuthServerStore _store;
        private readonly TimeProvider _timeProvider;
        private readonly IAuthAuditLogger? _auditLogger;

        /// <summary>
        /// Initializes the endpoint.
        /// </summary>
        /// <param name="store">The application's store.</param>
        /// <param name="timeProvider">The clock.</param>
        /// <param name="auditLogger">The audit logger, if registered.</param>
        public OAuthRegistrationEndpoint(IOAuthServerStore store, TimeProvider timeProvider, IAuthAuditLogger? auditLogger = null)
        {
            _store = store;
            _timeProvider = timeProvider;
            _auditLogger = auditLogger;
        }

        /// <summary>
        /// Handles a registration request.
        /// </summary>
        /// <param name="httpContext">The request.</param>
        /// <returns>201 with the registered client, or 400 with the RFC 7591 error.</returns>
        public async Task<IResult> HandleAsync(HttpContext httpContext)
        {
            httpContext.SetNoStoreCacheControl();
            CancellationToken cancellationToken = httpContext.RequestAborted;

            using JsonDocument? document = await OAuthJson.TryReadAsync(httpContext.Request.Body, OAuthJson.MaxRequestBodyBytes, cancellationToken);
            if (document == null)
            {
                return await RefuseAsync(httpContext, new OAuthError(OAuthError.InvalidClientMetadata, $"The body must be a JSON object of at most {OAuthJson.MaxRequestBodyBytes} bytes."));
            }

            if (!OAuthClientMetadata.TryRead(document.RootElement, out OAuthClientMetadata? metadata, out OAuthError? error))
            {
                return await RefuseAsync(httpContext, error);
            }

            // RFC 7591 section 2: a registration that names no method gets client_secret_basic.
            string methodName = metadata.AuthenticationMethodName ?? OAuthClientAuthenticationMethodNames.ClientSecretBasic;
            if (!OAuthClientAuthenticationMethodNames.TryParse(methodName, out OAuthClientAuthenticationMethod method))
            {
                return await RefuseAsync(httpContext, new OAuthError(
                    OAuthError.InvalidClientMetadata,
                    $"token_endpoint_auth_method must be one of {string.Join(", ", OAuthClientAuthenticationMethodNames.All)}."));
            }

            string clientId = SecureToken.Generate();
            string? clientSecret = method == OAuthClientAuthenticationMethod.None ? null : SecureToken.Generate();
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

            OAuthClient client = new OAuthClient
            {
                ClientId = clientId,
                ClientSecretHash = clientSecret == null ? null : SecureToken.Hash(clientSecret),
                AuthenticationMethod = method,
                RedirectUris = metadata.RedirectUris,
                ClientName = metadata.ClientName,
                ClientUri = metadata.ClientUri,
                CreatedAt = now,
            };
            await _store.StoreClientAsync(client, cancellationToken);

            if (_auditLogger != null)
            {
                await _auditLogger.OnOAuthClientRegistrationAsync(httpContext, new OAuthClientRegistrationResult
                {
                    Success = true,
                    ClientId = clientId,
                    ClientName = client.ClientName,
                    RedirectUris = client.RedirectUris,
                    AuthenticationMethod = method,
                });
            }

            // RFC 7591 section 3.2.1: the registered metadata, which is what this server supports whatever was asked.
            Dictionary<string, object?> response = new Dictionary<string, object?>
            {
                ["client_id"] = clientId,
                ["client_id_issued_at"] = new DateTimeOffset(now).ToUnixTimeSeconds(),
                ["client_secret"] = clientSecret,
                ["client_secret_expires_at"] = clientSecret == null ? null : 0,
                ["client_name"] = client.ClientName,
                ["client_uri"] = client.ClientUri,
                ["redirect_uris"] = client.RedirectUris,
                ["grant_types"] = new[] { "authorization_code", "refresh_token" },
                ["response_types"] = new[] { "code" },
                ["token_endpoint_auth_method"] = methodName,
            };
            // A dictionary's null entries are written whatever the ignore condition, so they are left out here.
            Dictionary<string, object> written = response.Where(entry => entry.Value != null).ToDictionary(entry => entry.Key, entry => entry.Value!);
            return Results.Json(written, OAuthJson.Options, statusCode: StatusCodes.Status201Created);
        }

        private async Task<IResult> RefuseAsync(HttpContext httpContext, OAuthError error)
        {
            if (_auditLogger != null)
            {
                await _auditLogger.OnOAuthClientRegistrationAsync(httpContext, new OAuthClientRegistrationResult
                {
                    Success = false,
                    FailureCode = error.Code,
                });
            }

            return error.ToResult();
        }
    }
}
