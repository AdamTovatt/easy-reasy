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
        /// <returns>200 for any token, known or not (RFC 7009 section 2.2), 401 for a client that does not authenticate.</returns>
        public async Task<IResult> RevokeAsync(HttpContext httpContext)
        {
            httpContext.SetNoStoreCacheControl();
            CancellationToken cancellationToken = httpContext.RequestAborted;

            if (!httpContext.Request.HasFormContentType)
            {
                return new OAuthError(OAuthError.InvalidRequest, "The body must be application/x-www-form-urlencoded.").ToResult();
            }

            OAuthParameters form = new OAuthParameters(await httpContext.Request.ReadFormAsync(cancellationToken));
            string? repeated = form.FirstRepeated;
            if (repeated != null)
            {
                return new OAuthError(OAuthError.InvalidRequest, $"The parameter '{repeated}' was sent more than once.").ToResult();
            }

            OAuthClientAuthentication client = await _authenticator.AuthenticateAsync(httpContext, form, cancellationToken);
            if (!client.Success)
            {
                return client.ToResult(httpContext);
            }

            string? token = form.Get("token");
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
