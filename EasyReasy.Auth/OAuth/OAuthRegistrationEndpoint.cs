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
                await _auditLogger.OnOAuthClientRegistrationAsync(
                    httpContext,
                    OAuthClientRegistrationResult.Succeeded(clientId, client.ClientName, client.RedirectUris, method));
            }

            OAuthClientRegistrationResponse response = new OAuthClientRegistrationResponse
            {
                ClientId = clientId,
                ClientIdIssuedAt = new DateTimeOffset(now).ToUnixTimeSeconds(),
                ClientSecret = clientSecret,
                ClientName = client.ClientName,
                ClientUri = client.ClientUri,
                RedirectUris = client.RedirectUris,
                TokenEndpointAuthMethod = methodName,
            };
            return Results.Json(response, OAuthJson.Options, statusCode: StatusCodes.Status201Created);
        }

        private async Task<IResult> RefuseAsync(HttpContext httpContext, OAuthError error)
        {
            if (_auditLogger != null)
            {
                await _auditLogger.OnOAuthClientRegistrationAsync(httpContext, OAuthClientRegistrationResult.Failed(error.Code));
            }

            return error.ToResult();
        }
    }
}
