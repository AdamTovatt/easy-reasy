using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The token endpoint: redeeming a code and refreshing. Refreshing runs <see cref="RefreshTokenService"/> over the client's refresh tokens, so rotation and reuse detection
    /// are the library's one implementation of them.
    /// </summary>
    internal sealed class OAuthTokenEndpoint
    {
        private readonly OAuthServerSettings _settings;
        private readonly IOAuthServerStore _store;
        private readonly OAuthClientAuthenticator _authenticator;
        private readonly IOAuthGrantHandler _handler;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly TimeProvider _timeProvider;
        private readonly IAuthAuditLogger? _auditLogger;

        /// <summary>
        /// Initializes the endpoint.
        /// </summary>
        /// <param name="settings">The server's settings.</param>
        /// <param name="store">The application's store.</param>
        /// <param name="authenticator">The client authenticator.</param>
        /// <param name="handler">The application's grant handler.</param>
        /// <param name="jwtTokenService">The token issuer.</param>
        /// <param name="timeProvider">The clock.</param>
        /// <param name="auditLogger">The audit logger, if registered.</param>
        public OAuthTokenEndpoint(
            OAuthServerSettings settings,
            IOAuthServerStore store,
            OAuthClientAuthenticator authenticator,
            IOAuthGrantHandler handler,
            IJwtTokenService jwtTokenService,
            TimeProvider timeProvider,
            IAuthAuditLogger? auditLogger = null)
        {
            _settings = settings;
            _store = store;
            _authenticator = authenticator;
            _handler = handler;
            _jwtTokenService = jwtTokenService;
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

            if (!httpContext.Request.HasFormContentType)
            {
                return await RefuseAsync(httpContext, null, null, OAuthTokenFailureReason.InvalidRequest, OAuthError.InvalidRequest, "The body must be application/x-www-form-urlencoded.");
            }

            OAuthParameters form = new OAuthParameters(await httpContext.Request.ReadFormAsync(cancellationToken));
            string? repeated = form.FirstRepeated;
            if (repeated != null)
            {
                return await RefuseAsync(httpContext, null, null, OAuthTokenFailureReason.InvalidRequest, OAuthError.InvalidRequest, $"The parameter '{repeated}' was sent more than once.");
            }

            OAuthGrantType? grantType = form.Get("grant_type") switch
            {
                "authorization_code" => OAuthGrantType.AuthorizationCode,
                "refresh_token" => OAuthGrantType.RefreshToken,
                _ => null,
            };

            OAuthClientAuthentication client = await _authenticator.AuthenticateAsync(httpContext, form, cancellationToken);
            if (!client.Success)
            {
                await AuditAsync(httpContext, new OAuthTokenResult
                {
                    Success = false,
                    GrantType = grantType,
                    ClientId = client.ClientId,
                    FailureReason = OAuthTokenFailureReason.InvalidClient,
                });
                return client.ToResult(httpContext);
            }

            if (grantType == null)
            {
                return form.Get("grant_type") == null
                    ? await RefuseAsync(httpContext, null, client.ClientId, OAuthTokenFailureReason.InvalidRequest, OAuthError.InvalidRequest, "grant_type is required.")
                    : await RefuseAsync(httpContext, null, client.ClientId, OAuthTokenFailureReason.UnsupportedGrantType, OAuthError.UnsupportedGrantType, "grant_type must be authorization_code or refresh_token.");
            }

            string? resource = form.Get("resource");
            if (resource != null && !_settings.IsOwnResource(resource))
            {
                return await RefuseAsync(httpContext, grantType, client.ClientId, OAuthTokenFailureReason.ResourceMismatch, OAuthError.InvalidTarget, "resource is not this server's.");
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
                return await RefuseAsync(httpContext, OAuthGrantType.AuthorizationCode, clientId, OAuthTokenFailureReason.InvalidRequest, OAuthError.InvalidRequest, "code, redirect_uri and code_verifier are required.");
            }

            string codeHash = SecureToken.Hash(rawCode);
            OAuthAuthorizationCode? code = await _store.GetAuthorizationCodeAsync(codeHash, cancellationToken);
            if (code == null)
            {
                return await RefuseAsync(httpContext, OAuthGrantType.AuthorizationCode, clientId, OAuthTokenFailureReason.CodeNotFound, OAuthError.InvalidGrant, "The code is invalid.");
            }

            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

            // Each refusal from here on names the code's grant, subject and family for the audit record.
            async Task<IResult> RefuseCodeAsync(OAuthTokenFailureReason reason, string error = OAuthError.InvalidGrant)
            {
                await AuditAsync(httpContext, new OAuthTokenResult
                {
                    Success = false,
                    GrantType = OAuthGrantType.AuthorizationCode,
                    ClientId = clientId,
                    GrantId = code.GrantId,
                    Subject = code.Subject,
                    FamilyId = code.FamilyId,
                    FailureReason = reason,
                });
                return new OAuthError(error, error == OAuthError.InvalidTarget ? "resource is not the authorization request's." : "The code is invalid.").ToResult();
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
            SessionStartResult session = await CreateRefreshTokenService(adapter, claimsResolver: null).StartSessionWithFamilyAsync(
                code.FamilyId,
                _jwtTokenService,
                code.Subject,
                code.AuthType,
                OAuthServerClaims.WithClientId(RefreshTokenClaims.DeserializeClaims(code.SerializedClaims), clientId),
                RefreshTokenClaims.DeserializeRoles(code.SerializedRoles),
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

            await AuditAsync(httpContext, new OAuthTokenResult
            {
                Success = true,
                GrantType = OAuthGrantType.AuthorizationCode,
                ClientId = clientId,
                GrantId = code.GrantId,
                Subject = code.Subject,
                FamilyId = session.FamilyId,
            });

            httpContext.MarkAuthenticationSucceeded();
            return TokenResponse(session.AuthResponse);
        }

        private async Task<IResult> RefreshAsync(HttpContext httpContext, OAuthParameters form, string clientId, CancellationToken cancellationToken)
        {
            string? refreshToken = form.Get("refresh_token");
            if (refreshToken == null)
            {
                return await RefuseAsync(httpContext, OAuthGrantType.RefreshToken, clientId, OAuthTokenFailureReason.InvalidRequest, OAuthError.InvalidRequest, "refresh_token is required.");
            }

            // The token's row is read here first, so the grant the refresh runs under is known before the service starts,
            // not learned from inside it. Another client's token reads as unknown, as it does to the adapter.
            OAuthRefreshToken? row = await _store.GetRefreshTokenAsync(SecureToken.Hash(refreshToken), cancellationToken);
            if (row == null || !string.Equals(row.ClientId, clientId, StringComparison.Ordinal))
            {
                return await RefuseAsync(httpContext, OAuthGrantType.RefreshToken, clientId, OAuthTokenFailureReason.RefreshTokenNotFound, OAuthError.InvalidGrant, "The refresh token is invalid.");
            }

            string grantId = row.GrantId;
            OAuthRefreshTokenStoreAdapter adapter = new OAuthRefreshTokenStoreAdapter(_store, clientId, grantId);
            RefreshTokenService service = CreateRefreshTokenService(adapter, new OAuthRefreshClaimsResolver(_handler, grantId, clientId, httpContext));

            RefreshResult result;
            try
            {
                result = await service.RefreshAsync(refreshToken, _jwtTokenService, httpContext, cancellationToken);
            }
            catch
            {
                // The audit trail records every outcome, a throwing handler's too, before the exception propagates.
                await AuditAsync(httpContext, new OAuthTokenResult
                {
                    Success = false,
                    GrantType = OAuthGrantType.RefreshToken,
                    ClientId = clientId,
                    GrantId = grantId,
                    FailureReason = OAuthTokenFailureReason.HandlerError,
                });
                throw;
            }

            await AuditAsync(httpContext, new OAuthTokenResult
            {
                Success = result.Success,
                GrantType = OAuthGrantType.RefreshToken,
                ClientId = clientId,
                GrantId = grantId,
                Subject = result.Subject,
                FamilyId = result.FamilyId,
                FailureReason = result.Success ? null : ToTokenFailureReason(result.FailureReason!.Value),
            });

            if (!result.Success)
            {
                return new OAuthError(OAuthError.InvalidGrant, "The refresh token is invalid.").ToResult();
            }

            httpContext.MarkAuthenticationSucceeded();
            return TokenResponse(result.AuthResponse!);
        }

        // A service per request, over the client's refresh tokens: no audit logger, since a client's refresh is reported
        // to OnOAuthTokenAsync and never to the first-party OnRefreshAsync, and AllowMultiple, since a client's
        // connections are never revoked by subject.
        private RefreshTokenService CreateRefreshTokenService(OAuthRefreshTokenStoreAdapter adapter, IRefreshClaimsResolver? claimsResolver)
        {
            return new RefreshTokenService(
                adapter,
                _settings.RefreshTokenLifetime,
                _settings.AccessTokenLifetime,
                auditLogger: null,
                claimsResolver,
                ConcurrentSessionPolicy.AllowMultiple,
                _timeProvider);
        }

        private IResult TokenResponse(AuthResponse authResponse)
        {
            Dictionary<string, object?> body = new Dictionary<string, object?>
            {
                ["access_token"] = authResponse.Token,
                ["token_type"] = "Bearer",
                ["expires_in"] = (long)_settings.AccessTokenLifetime.TotalSeconds,
                ["refresh_token"] = authResponse.RefreshToken,
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

        private async Task<IResult> RefuseAsync(HttpContext httpContext, OAuthGrantType? grantType, string? clientId, OAuthTokenFailureReason reason, string error, string description)
        {
            await AuditAsync(httpContext, new OAuthTokenResult
            {
                Success = false,
                GrantType = grantType,
                ClientId = clientId,
                FailureReason = reason,
            });
            return new OAuthError(error, description).ToResult();
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
