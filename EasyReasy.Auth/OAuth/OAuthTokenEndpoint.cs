using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The token endpoint: redeeming a code and refreshing. Both run <see cref="RefreshTokenRotation"/> over the client's
    /// refresh tokens, so a family's creation, rotation and reuse detection are the library's one implementation of them.
    /// </summary>
    internal sealed class OAuthTokenEndpoint
    {
        private readonly OAuthServerSettings _settings;
        private readonly IOAuthServerStore _store;
        private readonly OAuthClientAuthenticator _authenticator;
        private readonly IOAuthGrantHandler _handler;
        private readonly IJwtTokenService _accessTokens;
        private readonly TimeProvider _timeProvider;
        private readonly IAuthAuditLogger? _auditLogger;

        /// <summary>
        /// Initializes the endpoint.
        /// </summary>
        /// <param name="settings">The server's settings.</param>
        /// <param name="store">The application's store.</param>
        /// <param name="authenticator">The client authenticator.</param>
        /// <param name="handler">The application's grant handler.</param>
        /// <param name="accessTokens">The issuer of connection access tokens, whose audience is the resource.</param>
        /// <param name="timeProvider">The clock.</param>
        /// <param name="auditLogger">The audit logger, if registered.</param>
        public OAuthTokenEndpoint(
            OAuthServerSettings settings,
            IOAuthServerStore store,
            OAuthClientAuthenticator authenticator,
            IOAuthGrantHandler handler,
            IJwtTokenService accessTokens,
            TimeProvider timeProvider,
            IAuthAuditLogger? auditLogger = null)
        {
            _settings = settings;
            _store = store;
            _authenticator = authenticator;
            _handler = handler;
            _accessTokens = accessTokens;
            _timeProvider = timeProvider;
            _auditLogger = auditLogger;
        }

        /// <summary>
        /// Handles a token request.
        /// </summary>
        /// <param name="httpContext">The request.</param>
        /// <returns>200 with the tokens, or the RFC 6749 error.</returns>
        public async Task<IResult> TokenAsync(HttpContext httpContext)
        {
            httpContext.SetNoStoreCacheControl();
            httpContext.Response.Headers.Pragma = "no-cache";
            CancellationToken cancellationToken = httpContext.RequestAborted;

            OAuthClientRequest request = await _authenticator.ReadRequestAsync(httpContext, cancellationToken);
            if (!request.IsWellFormed)
            {
                return await RefuseAsync(httpContext, OAuthTokenResult.Failed(OAuthTokenFailureReason.InvalidRequest, null, null), request.RequestError);
            }

            OAuthParameters form = request.Form;
            OAuthGrantType? grantType = form.Get("grant_type") switch
            {
                "authorization_code" => OAuthGrantType.AuthorizationCode,
                "refresh_token" => OAuthGrantType.RefreshToken,
                _ => null,
            };

            OAuthClientAuthentication client = request.Client;
            if (!client.Success)
            {
                await AuditAsync(httpContext, OAuthTokenResult.Failed(OAuthTokenFailureReason.InvalidClient, grantType, client.ClientId));
                return client.ToResult(httpContext);
            }

            if (grantType == null)
            {
                return form.Get("grant_type") == null
                    ? await RefuseAsync(httpContext, OAuthTokenResult.Failed(OAuthTokenFailureReason.InvalidRequest, null, client.ClientId), new OAuthError(OAuthError.InvalidRequest, "grant_type is required."))
                    : await RefuseAsync(httpContext, OAuthTokenResult.Failed(OAuthTokenFailureReason.UnsupportedGrantType, null, client.ClientId), new OAuthError(OAuthError.UnsupportedGrantType, "grant_type must be authorization_code or refresh_token."));
            }

            string? resource = form.Get("resource");
            if (resource != null && !_settings.IsOwnResource(resource))
            {
                return await RefuseAsync(httpContext, OAuthTokenResult.Failed(OAuthTokenFailureReason.ResourceMismatch, grantType, client.ClientId), new OAuthError(OAuthError.InvalidTarget, "resource is not this server's."));
            }

            return grantType == OAuthGrantType.AuthorizationCode
                ? await RedeemCodeAsync(httpContext, form, client.ClientId, resource, cancellationToken)
                : await RefreshAsync(httpContext, form, client.ClientId, cancellationToken);
        }

        private async Task<IResult> RedeemCodeAsync(HttpContext httpContext, OAuthParameters form, string clientId, string? resource, CancellationToken cancellationToken)
        {
            string? rawCode = form.Get("code");
            string? redirectUri = form.Get("redirect_uri");
            string? codeVerifier = form.Get("code_verifier");
            if (rawCode == null || redirectUri == null || codeVerifier == null)
            {
                return await RefuseAsync(httpContext, OAuthTokenResult.Failed(OAuthTokenFailureReason.InvalidRequest, OAuthGrantType.AuthorizationCode, clientId), new OAuthError(OAuthError.InvalidRequest, "code, redirect_uri and code_verifier are required."));
            }

            string codeHash = SecureToken.Hash(rawCode);
            OAuthAuthorizationCode? code = await _store.GetAuthorizationCodeAsync(codeHash, cancellationToken);
            if (code == null)
            {
                return await RefuseAsync(httpContext, OAuthTokenResult.Failed(OAuthTokenFailureReason.CodeNotFound, OAuthGrantType.AuthorizationCode, clientId), new OAuthError(OAuthError.InvalidGrant, "The code is invalid."));
            }

            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

            // Each refusal from here on names the code's grant, subject and family for the audit record.
            Task<IResult> RefuseCodeAsync(OAuthTokenFailureReason reason, string error = OAuthError.InvalidGrant)
            {
                return RefuseAsync(
                    httpContext,
                    OAuthTokenResult.Failed(reason, OAuthGrantType.AuthorizationCode, clientId, code.GrantId, code.Subject, code.FamilyId),
                    new OAuthError(error, error == OAuthError.InvalidTarget ? "resource is not the authorization request's." : "The code is invalid."));
            }

            // A code presented after it was redeemed: one of the two presenters is not the client, so whatever the first
            // redemption issued is invalidated (OAuth 2.1 section 4.1.3). The code is marked first, as its own write, so
            // a first redemption still under way sees it when it checks the code again below.
            if (code.RedeemedAt != null || !await _store.RedeemAuthorizationCodeAsync(codeHash, now, cancellationToken))
            {
                await _store.InvalidateAuthorizationCodeAsync(codeHash, cancellationToken);
                await _store.InvalidateRefreshTokenFamilyAsync(code.FamilyId, cancellationToken);
                return await RefuseCodeAsync(OAuthTokenFailureReason.CodeReused);
            }

            // The code is used up before anything else is checked, so a wrong verifier or redirect URI cannot be retried
            // against it.
            if (code.IsInvalidated)
            {
                return await RefuseCodeAsync(OAuthTokenFailureReason.CodeInvalidated);
            }

            if (code.ExpiresAt <= now)
            {
                return await RefuseCodeAsync(OAuthTokenFailureReason.CodeExpired);
            }

            if (!string.Equals(code.ClientId, clientId, StringComparison.Ordinal))
            {
                return await RefuseCodeAsync(OAuthTokenFailureReason.ClientMismatch);
            }

            if (!string.Equals(code.RedirectUri, redirectUri, StringComparison.Ordinal))
            {
                return await RefuseCodeAsync(OAuthTokenFailureReason.RedirectUriMismatch);
            }

            if (!OAuthPkce.VerifierMatchesChallenge(codeVerifier, code.CodeChallenge))
            {
                return await RefuseCodeAsync(OAuthTokenFailureReason.VerifierMismatch);
            }

            if (resource != null && code.Resource != null && !string.Equals(resource, code.Resource, StringComparison.Ordinal))
            {
                return await RefuseCodeAsync(OAuthTokenFailureReason.ResourceMismatch, OAuthError.InvalidTarget);
            }

            OAuthRefreshTokenStoreAdapter adapter = new OAuthRefreshTokenStoreAdapter(_store, clientId, code.GrantId);
            SessionStartResult session = await CreateRotation(adapter).StartSessionAsync(
                code.FamilyId,
                _accessTokens,
                code.Subject,
                code.AuthType,
                RefreshTokenRotation.SerializeSessionClaims(OAuthServerClaims.WithClientId(RefreshTokenClaims.DeserializeClaims(code.SerializedClaims), clientId)),
                code.SerializedRoles,
                cancellationToken);

            // The code is read again now the refresh token is stored. A grant revocation or a second presentation that
            // came in during this redemption marked the code before sweeping refresh tokens, so either it shows here, or
            // the sweep ran after this token was stored and caught it. Either way nothing issued here outlives it.
            OAuthAuthorizationCode? codeNow = await _store.GetAuthorizationCodeAsync(codeHash, cancellationToken);
            if (codeNow == null || codeNow.IsInvalidated)
            {
                await _store.InvalidateRefreshTokenFamilyAsync(code.FamilyId, cancellationToken);
                return await RefuseCodeAsync(OAuthTokenFailureReason.CodeInvalidated);
            }

            await AuditAsync(httpContext, OAuthTokenResult.Succeeded(OAuthGrantType.AuthorizationCode, clientId, code.GrantId, code.Subject, session.FamilyId));

            httpContext.MarkAuthenticationSucceeded();
            return TokenResponse(session.AuthResponse);
        }

        private async Task<IResult> RefreshAsync(HttpContext httpContext, OAuthParameters form, string clientId, CancellationToken cancellationToken)
        {
            string? refreshToken = form.Get("refresh_token");
            if (refreshToken == null)
            {
                return await RefuseAsync(httpContext, OAuthTokenResult.Failed(OAuthTokenFailureReason.InvalidRequest, OAuthGrantType.RefreshToken, clientId), new OAuthError(OAuthError.InvalidRequest, "refresh_token is required."));
            }

            // The token's row is read here first, so the grant the refresh runs under is known before the rotation starts,
            // not learned from inside it. Another client's token reads as unknown, as it does to the adapter.
            OAuthRefreshToken? row = await _store.GetRefreshTokenAsync(SecureToken.Hash(refreshToken), cancellationToken);
            if (row == null || !string.Equals(row.ClientId, clientId, StringComparison.Ordinal))
            {
                return await RefuseAsync(httpContext, OAuthTokenResult.Failed(OAuthTokenFailureReason.RefreshTokenNotFound, OAuthGrantType.RefreshToken, clientId), new OAuthError(OAuthError.InvalidGrant, "The refresh token is invalid."));
            }

            string grantId = row.GrantId;
            OAuthRefreshTokenStoreAdapter adapter = new OAuthRefreshTokenStoreAdapter(_store, clientId, grantId);
            OAuthRefreshClaimsResolver claimsResolver = new OAuthRefreshClaimsResolver(_handler, grantId, clientId, httpContext);

            RefreshResult result;
            try
            {
                result = await CreateRotation(adapter).RotateAsync(refreshToken, _accessTokens, claimsResolver, httpContext, cancellationToken);
            }
            catch
            {
                // The audit trail records every outcome, a throwing handler's too, before the exception propagates.
                await AuditAsync(httpContext, OAuthTokenResult.Failed(OAuthTokenFailureReason.HandlerError, OAuthGrantType.RefreshToken, clientId, grantId));
                throw;
            }

            if (!result.Success)
            {
                return await RefuseAsync(
                    httpContext,
                    OAuthTokenResult.Failed(ToTokenFailureReason(result.FailureReason.Value), OAuthGrantType.RefreshToken, clientId, grantId, result.Subject, result.FamilyId),
                    new OAuthError(OAuthError.InvalidGrant, "The refresh token is invalid."));
            }

            await AuditAsync(httpContext, OAuthTokenResult.Succeeded(OAuthGrantType.RefreshToken, clientId, grantId, result.Subject, result.FamilyId));
            httpContext.MarkAuthenticationSucceeded();
            return TokenResponse(result.AuthResponse);
        }

        // The rotation alone, without the first-party service's session policy and audit hooks: a client's refresh is
        // reported to OnOAuthTokenAsync, and its connections are never revoked by subject.
        private RefreshTokenRotation CreateRotation(OAuthRefreshTokenStoreAdapter adapter)
        {
            return new RefreshTokenRotation(adapter, _settings.RefreshTokenLifetime, _settings.AccessTokenLifetime, _timeProvider);
        }

        private IResult TokenResponse(AuthResponse authResponse)
        {
            OAuthTokenResponse body = new OAuthTokenResponse
            {
                AccessToken = authResponse.Token,
                ExpiresIn = (long)_settings.AccessTokenLifetime.TotalSeconds,
                // The refresh token service always issues one alongside the access token.
                RefreshToken = authResponse.RefreshToken ?? throw new InvalidOperationException("The refresh token service issued no refresh token."),
            };
            return Results.Json(body, OAuthJson.Options);
        }

        private static OAuthTokenFailureReason ToTokenFailureReason(RefreshFailureReason reason)
        {
            return reason switch
            {
                RefreshFailureReason.TokenNotFound => OAuthTokenFailureReason.RefreshTokenNotFound,
                RefreshFailureReason.TokenExpired => OAuthTokenFailureReason.RefreshTokenExpired,
                RefreshFailureReason.TokenInvalidated => OAuthTokenFailureReason.RefreshTokenInvalidated,
                RefreshFailureReason.TheftDetected => OAuthTokenFailureReason.RefreshTokenReused,
                RefreshFailureReason.DeniedByResolver => OAuthTokenFailureReason.DeniedByHandler,
                RefreshFailureReason.ResolverError => OAuthTokenFailureReason.HandlerError,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Not a refresh failure the token endpoint expects."),
            };
        }

        private async Task<IResult> RefuseAsync(HttpContext httpContext, OAuthTokenResult refusal, OAuthError error)
        {
            await AuditAsync(httpContext, refusal);
            return error.ToResult();
        }

        private async Task AuditAsync(HttpContext httpContext, OAuthTokenResult result)
        {
            if (_auditLogger != null)
            {
                await _auditLogger.OnOAuthTokenAsync(httpContext, result);
            }
        }
    }
}
