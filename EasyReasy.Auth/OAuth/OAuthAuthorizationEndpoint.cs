using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The front channel: the authorization endpoint the client sends the browser to, and the endpoints the application's
    /// consent page reads and completes the pending request through.
    /// </summary>
    internal sealed class OAuthAuthorizationEndpoint
    {
        /// <summary>
        /// The longest <c>state</c>, <c>scope</c> or <c>resource</c> an authorization request may carry: the values stored
        /// from an anonymous request, bounded like a redirect URI.
        /// </summary>
        public const int MaxStoredParameterLength = OAuthRedirectUri.MaxLength;

        private readonly OAuthServerSettings _settings;
        private readonly IOAuthServerStore _store;
        private readonly OAuthClientDirectory _clients;
        private readonly IOAuthGrantHandler _handler;
        private readonly TimeProvider _timeProvider;
        private readonly IAuthAuditLogger? _auditLogger;

        /// <summary>
        /// Initializes the endpoint.
        /// </summary>
        /// <param name="settings">The server's settings.</param>
        /// <param name="store">The application's store.</param>
        /// <param name="clients">The client directory.</param>
        /// <param name="handler">The application's grant handler.</param>
        /// <param name="timeProvider">The clock.</param>
        /// <param name="auditLogger">The audit logger, if registered.</param>
        public OAuthAuthorizationEndpoint(
            OAuthServerSettings settings,
            IOAuthServerStore store,
            OAuthClientDirectory clients,
            IOAuthGrantHandler handler,
            TimeProvider timeProvider,
            IAuthAuditLogger? auditLogger = null)
        {
            _settings = settings;
            _store = store;
            _clients = clients;
            _handler = handler;
            _timeProvider = timeProvider;
            _auditLogger = auditLogger;
        }

        /// <summary>
        /// Handles an authorization request: validates it and sends the browser to the consent page.
        /// </summary>
        /// <remarks>
        /// Until the client and its redirect URI are known good, an error is answered here with a 400 and never redirected
        /// (RFC 6749 section 4.1.2.1), so the endpoint cannot be used to send a browser to an arbitrary address. After that,
        /// errors go back to the client's redirect URI with its <c>state</c> and the issuer.
        /// </remarks>
        /// <param name="httpContext">The request.</param>
        /// <returns>A redirect to the consent page or to the client with an error, or a 400.</returns>
        public async Task<IResult> AuthorizeAsync(HttpContext httpContext)
        {
            httpContext.SetNoStoreCacheControl();
            CancellationToken cancellationToken = httpContext.RequestAborted;
            OAuthParameters query = new OAuthParameters(httpContext.Request.Query);

            // A repeated parameter is invalid (RFC 6749 section 3.1), but which client and redirect URI it would go to is
            // then unclear, so it is answered here rather than redirected.
            string? repeated = query.FirstRepeated;
            if (repeated != null)
            {
                return new OAuthError(OAuthError.InvalidRequest, $"The parameter '{repeated}' was sent more than once.").ToResult();
            }

            string? clientId = query.Get("client_id");
            if (clientId == null)
            {
                return new OAuthError(OAuthError.InvalidRequest, "client_id is required.").ToResult();
            }

            OAuthClientLookup lookup = await _clients.FindAsync(clientId, cancellationToken);
            if (!lookup.IsFound)
            {
                return new OAuthError(OAuthError.InvalidClient, lookup.Failure).ToResult();
            }

            // Required, though RFC 6749 lets a client with one registered URI leave it out: OAuth 2.1 and every MCP client
            // send it, and requiring it keeps the token request's comparison exact.
            string? redirectUri = query.Get("redirect_uri");
            if (redirectUri == null || !lookup.Client.AllowsRedirectUri(redirectUri))
            {
                return new OAuthError(OAuthError.InvalidRequest, "redirect_uri is missing or is not one the client registered.").ToResult();
            }

            // The three values stored from this anonymous request are bounded here, as registration bounds its fields, and
            // not left to the host's request-line limit. An oversized state is not echoed back.
            string? state = query.Get("state");
            string? scope = query.Get("scope");
            string? resource = query.Get("resource");
            if (state?.Length > MaxStoredParameterLength || scope?.Length > MaxStoredParameterLength || resource?.Length > MaxStoredParameterLength)
            {
                return RedirectError(redirectUri, null, OAuthError.InvalidRequest, $"state, scope and resource must each be at most {MaxStoredParameterLength} characters.");
            }

            if (query.Get("response_type") != "code")
            {
                return RedirectError(redirectUri, state, OAuthError.UnsupportedResponseType, "response_type must be code.");
            }

            string? codeChallenge = query.Get("code_challenge");
            if (codeChallenge == null)
            {
                return RedirectError(redirectUri, state, OAuthError.InvalidRequest, "code_challenge is required: every client uses PKCE.");
            }

            // An absent method means plain (RFC 7636 section 4.3), which OAuth 2.1 servers may refuse and this one does.
            if (query.Get("code_challenge_method") != "S256")
            {
                return RedirectError(redirectUri, state, OAuthError.InvalidRequest, "code_challenge_method must be S256.");
            }

            if (!OAuthPkce.IsS256Challenge(codeChallenge))
            {
                return RedirectError(redirectUri, state, OAuthError.InvalidRequest, "code_challenge must be the base64url SHA-256 of the verifier, 43 characters.");
            }

            if (resource != null && !_settings.IsOwnResource(resource))
            {
                return RedirectError(redirectUri, state, OAuthError.InvalidTarget, "resource is not this server's.");
            }

            string requestId = SecureToken.Generate();
            OAuthAuthorizationRequest request = new OAuthAuthorizationRequest
            {
                RequestHash = SecureToken.Hash(requestId),
                ClientId = lookup.Client.Info.ClientId,
                ClientName = lookup.Client.Info.ClientName,
                ClientUri = lookup.Client.Info.ClientUri,
                RedirectUri = redirectUri,
                State = state,
                CodeChallenge = codeChallenge,
                Scope = scope,
                Resource = resource,
                ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.Add(_settings.AuthorizationRequestLifetime),
            };
            await _store.StoreAuthorizationRequestAsync(request, cancellationToken);

            return Results.Redirect(OAuthUrl.AppendQuery(_settings.ConsentPageUrl, [new KeyValuePair<string, string?>("request", requestId)]));
        }

        /// <summary>
        /// Returns what a pending request asks, for the consent page.
        /// </summary>
        /// <param name="requestId">The request id from the consent page's URL.</param>
        /// <param name="httpContext">The request.</param>
        /// <returns>200 with <see cref="OAuthAuthorizationRequestDetails"/>, or 404.</returns>
        public async Task<IResult> GetRequestAsync(string requestId, HttpContext httpContext)
        {
            httpContext.SetNoStoreCacheControl();
            OAuthAuthorizationRequest? request = await FindPendingAsync(requestId, httpContext.RequestAborted);
            if (request == null)
            {
                return RequestNotFound();
            }

            OAuthAuthorizationRequestDetails details = new OAuthAuthorizationRequestDetails
            {
                Client = ClientInfo(request),
                RedirectUri = request.RedirectUri,
                Scopes = Scopes(request.Scope),
                Resource = request.Resource,
                ExpiresAt = request.ExpiresAt.ToString("O"),
            };
            return Results.Json(details, OAuthJson.Options);
        }

        /// <summary>
        /// Approves a pending request: the handler decides and records the grant, and the code is issued.
        /// </summary>
        /// <param name="requestId">The request id.</param>
        /// <param name="httpContext">The request, whose user is the approving person and whose body is the application's.</param>
        /// <returns>200 with <see cref="OAuthConsentRedirect"/>, 403 when the handler refuses, 404, or 400 for a body that is not JSON.</returns>
        public async Task<IResult> ApproveAsync(string requestId, HttpContext httpContext)
        {
            httpContext.SetNoStoreCacheControl();
            CancellationToken cancellationToken = httpContext.RequestAborted;

            // No body is no approval body; a body that is not JSON is the consent page's mistake, told as such.
            JsonElement? approvalBody = null;
            ReadOnlyMemory<byte>? bodyBytes = await OAuthJson.ReadLimitedAsync(httpContext.Request.Body, OAuthJson.MaxRequestBodyBytes, cancellationToken);
            if (bodyBytes == null || bodyBytes.Value.Length > 0)
            {
                using JsonDocument? document = bodyBytes == null ? null : OAuthJson.TryParse(bodyBytes.Value);
                if (document == null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        detail: $"The approval body must be JSON of at most {OAuthJson.MaxRequestBodyBytes} bytes.");
                }

                approvalBody = document.RootElement.Clone();
            }

            OAuthAuthorizationRequest? request = await FindPendingAsync(requestId, cancellationToken);
            if (request == null)
            {
                return RequestNotFound();
            }

            OAuthClientInfo client = ClientInfo(request);
            OAuthApprovalDecision decision = await _handler.ApproveAsync(new OAuthApprovalContext
            {
                HttpContext = httpContext,
                Client = client,
                RedirectUri = request.RedirectUri,
                RequestedScopes = Scopes(request.Scope),
                Resource = request.Resource,
                ApprovalBody = approvalBody,
            }, cancellationToken);

            if (decision.IsRefused)
            {
                await AuditAsync(httpContext, new OAuthAuthorizationResult
                {
                    Outcome = OAuthAuthorizationOutcome.Refused,
                    Client = client,
                    RefusalDetail = decision.RefusalDetail,
                });
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: decision.RefusalDetail);
            }

            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            if (!await _store.CompleteAuthorizationRequestAsync(request.RequestHash, now, cancellationToken))
            {
                return RequestNotFound();
            }

            OAuthGrant grant = decision.Grant;
            string code = SecureToken.Generate();
            await _store.StoreAuthorizationCodeAsync(new OAuthAuthorizationCode
            {
                CodeHash = SecureToken.Hash(code),
                ClientId = request.ClientId,
                RedirectUri = request.RedirectUri,
                CodeChallenge = request.CodeChallenge,
                Resource = request.Resource,
                GrantId = grant.GrantId,
                Subject = grant.Subject,
                AuthType = grant.AuthType,
                SerializedClaims = RefreshTokenClaims.SerializeClaims(OAuthServerClaims.Without(grant.Claims)),
                SerializedRoles = RefreshTokenClaims.SerializeRoles(grant.Roles),
                FamilyId = Guid.NewGuid().ToString(),
                ExpiresAt = now.Add(_settings.AuthorizationCodeLifetime),
            }, cancellationToken);

            await AuditAsync(httpContext, new OAuthAuthorizationResult
            {
                Outcome = OAuthAuthorizationOutcome.Approved,
                Client = client,
                GrantId = grant.GrantId,
                Subject = grant.Subject,
            });

            return Results.Json(new OAuthConsentRedirect
            {
                RedirectUri = OAuthUrl.AppendQuery(request.RedirectUri,
                [
                    new KeyValuePair<string, string?>("code", code),
                    new KeyValuePair<string, string?>("state", request.State),
                    new KeyValuePair<string, string?>("iss", _settings.Issuer),
                ]),
            }, OAuthJson.Options);
        }

        /// <summary>
        /// Denies a pending request: the client is told <c>access_denied</c>.
        /// </summary>
        /// <param name="requestId">The request id.</param>
        /// <param name="httpContext">The request.</param>
        /// <returns>200 with <see cref="OAuthConsentRedirect"/>, or 404.</returns>
        public async Task<IResult> DenyAsync(string requestId, HttpContext httpContext)
        {
            httpContext.SetNoStoreCacheControl();
            CancellationToken cancellationToken = httpContext.RequestAborted;

            OAuthAuthorizationRequest? request = await FindPendingAsync(requestId, cancellationToken);
            if (request == null
                || !await _store.CompleteAuthorizationRequestAsync(request.RequestHash, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken))
            {
                return RequestNotFound();
            }

            await AuditAsync(httpContext, new OAuthAuthorizationResult
            {
                Outcome = OAuthAuthorizationOutcome.Denied,
                Client = ClientInfo(request),
            });

            return Results.Json(new OAuthConsentRedirect
            {
                RedirectUri = RedirectErrorUrl(request.RedirectUri, request.State, OAuthError.AccessDenied, "The person denied the request."),
            }, OAuthJson.Options);
        }

        private async Task<OAuthAuthorizationRequest?> FindPendingAsync(string requestId, CancellationToken cancellationToken)
        {
            OAuthAuthorizationRequest? request = await _store.GetAuthorizationRequestAsync(SecureToken.Hash(requestId), cancellationToken);
            if (request == null || request.CompletedAt != null || request.ExpiresAt <= _timeProvider.GetUtcNow().UtcDateTime)
            {
                return null;
            }

            return request;
        }

        private static OAuthClientInfo ClientInfo(OAuthAuthorizationRequest request)
        {
            return OAuthClientInfo.Create(request.ClientId, request.ClientName, request.ClientUri);
        }

        private static IReadOnlyList<string> Scopes(string? scope)
        {
            return scope == null ? [] : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        private static IResult RequestNotFound()
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "The authorization request is unknown, expired or already completed.");
        }

        private IResult RedirectError(string redirectUri, string? state, string error, string description)
        {
            return Results.Redirect(RedirectErrorUrl(redirectUri, state, error, description));
        }

        private string RedirectErrorUrl(string redirectUri, string? state, string error, string description)
        {
            return OAuthUrl.AppendQuery(redirectUri,
            [
                new KeyValuePair<string, string?>("error", error),
                new KeyValuePair<string, string?>("error_description", description),
                new KeyValuePair<string, string?>("state", state),
                new KeyValuePair<string, string?>("iss", _settings.Issuer),
            ]);
        }

        private async Task AuditAsync(HttpContext httpContext, OAuthAuthorizationResult result)
        {
            if (_auditLogger != null)
            {
                await _auditLogger.OnOAuthAuthorizationAsync(httpContext, result);
            }
        }
    }
}
