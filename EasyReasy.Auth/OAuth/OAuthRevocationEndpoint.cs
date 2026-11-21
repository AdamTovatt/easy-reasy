using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The revocation endpoint (RFC 7009): a client ends one of its connections by revoking a refresh token, which
    /// invalidates that token's family.
    /// </summary>
    internal sealed class OAuthRevocationEndpoint
    {
        private readonly IOAuthServerStore _store;
        private readonly OAuthClientAuthenticator _authenticator;
        private readonly IAuthAuditLogger? _auditLogger;

        /// <summary>
        /// Initializes the endpoint.
        /// </summary>
        /// <param name="store">The application's store.</param>
        /// <param name="authenticator">The client authenticator.</param>
        /// <param name="auditLogger">The audit logger, if registered.</param>
        public OAuthRevocationEndpoint(IOAuthServerStore store, OAuthClientAuthenticator authenticator, IAuthAuditLogger? auditLogger = null)
        {
            _store = store;
            _authenticator = authenticator;
            _auditLogger = auditLogger;
        }

        /// <summary>
        /// Handles a revocation request: a refresh token of the calling client has its family invalidated.
        /// </summary>
        /// <param name="httpContext">The request.</param>
        /// <returns>200 for any token, known or not (RFC 7009 section 2.2), 401 <c>invalid_client</c> for a client that
        /// does not authenticate, 400 <c>invalid_request</c> for a body that is not a form or names no token.</returns>
        public async Task<IResult> RevokeAsync(HttpContext httpContext)
        {
            httpContext.SetNoStoreCacheControl();
            CancellationToken cancellationToken = httpContext.RequestAborted;

            OAuthClientRequest request = await _authenticator.ReadRequestAsync(httpContext, cancellationToken);
            if (!request.IsWellFormed)
            {
                return request.RequestError.ToResult();
            }

            OAuthClientAuthentication client = request.Client;
            if (!client.Success)
            {
                return client.ToResult(httpContext);
            }

            string? token = request.Form.Get("token");
            if (token == null)
            {
                return new OAuthError(OAuthError.InvalidRequest, "token is required.").ToResult();
            }

            // token_type_hint is only a hint (RFC 7009 section 2.1), and only refresh tokens can be revoked: an access
            // token is a signed token checked without a lookup, and expires on its own. Another client's token is left
            // alone and answered the same 200, so the response says nothing about which tokens exist.
            OAuthRefreshToken? refreshToken = await _store.GetRefreshTokenAsync(SecureToken.Hash(token), cancellationToken);
            OAuthRefreshToken? ownToken = refreshToken != null && string.Equals(refreshToken.ClientId, client.ClientId, StringComparison.Ordinal)
                ? refreshToken
                : null;
            if (ownToken != null)
            {
                await _store.InvalidateRefreshTokenFamilyAsync(ownToken.FamilyId, cancellationToken);
            }

            if (_auditLogger != null)
            {
                await _auditLogger.OnOAuthRevocationAsync(httpContext, new OAuthRevocationResult
                {
                    ClientId = client.ClientId,
                    WasKnown = ownToken != null,
                    GrantId = ownToken?.GrantId,
                    Subject = ownToken?.Subject,
                    FamilyId = ownToken?.FamilyId,
                });
            }

            return Results.Ok();
        }
    }
}
