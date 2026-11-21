namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// An <see cref="IOAuthServerStore"/> held in memory, for tests: an app's test host registers it in place of its database
    /// store to drive the authorization server end to end. Thread-safe, atomic where the interface requires it, and
    /// returning copies, so a row read before a change does not change under the reader, as with a database. For test
    /// projects only: nothing survives the process.
    /// </summary>
    public sealed class InMemoryOAuthServerStore : IOAuthServerStore
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, OAuthClient> _clients = new Dictionary<string, OAuthClient>(StringComparer.Ordinal);
        private readonly Dictionary<string, OAuthAuthorizationRequest> _requests = new Dictionary<string, OAuthAuthorizationRequest>(StringComparer.Ordinal);
        private readonly Dictionary<string, OAuthAuthorizationCode> _codes = new Dictionary<string, OAuthAuthorizationCode>(StringComparer.Ordinal);
        private readonly Dictionary<string, OAuthRefreshToken> _refreshTokens = new Dictionary<string, OAuthRefreshToken>(StringComparer.Ordinal);

        /// <summary>
        /// Every client stored, for a test's assertions.
        /// </summary>
        public IReadOnlyList<OAuthClient> Clients
        {
            get
            {
                lock (_lock)
                {
                    return _clients.Values.ToList();
                }
            }
        }

        /// <summary>
        /// Every authorization request stored, completed ones included, copied, for a test's assertions.
        /// </summary>
        public IReadOnlyList<OAuthAuthorizationRequest> AuthorizationRequests
        {
            get
            {
                lock (_lock)
                {
                    return _requests.Values.Select(Copy).ToList();
                }
            }
        }

        /// <summary>
        /// Every authorization code stored, redeemed ones included, copied, for a test's assertions.
        /// </summary>
        public IReadOnlyList<OAuthAuthorizationCode> AuthorizationCodes
        {
            get
            {
                lock (_lock)
                {
                    return _codes.Values.Select(Copy).ToList();
                }
            }
        }

        /// <summary>
        /// Every refresh token stored, consumed and invalidated ones included, copied, for a test's assertions.
        /// </summary>
        public IReadOnlyList<OAuthRefreshToken> RefreshTokens
        {
            get
            {
                lock (_lock)
                {
                    return _refreshTokens.Values.Select(Copy).ToList();
                }
            }
        }

        /// <inheritdoc />
        public Task StoreClientAsync(OAuthClient client, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            lock (_lock)
            {
                _clients.Add(client.ClientId, client);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<OAuthClient?> GetClientAsync(string clientId, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult(_clients.GetValueOrDefault(clientId));
            }
        }

        /// <inheritdoc />
        public Task StoreAuthorizationRequestAsync(OAuthAuthorizationRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            lock (_lock)
            {
                _requests.Add(request.RequestHash, Copy(request));
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<OAuthAuthorizationRequest?> GetAuthorizationRequestAsync(string requestHash, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult(_requests.TryGetValue(requestHash, out OAuthAuthorizationRequest? request) ? Copy(request) : null);
            }
        }

        /// <inheritdoc />
        public Task<bool> CompleteAuthorizationRequestAsync(string requestHash, DateTime completedAt, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (!_requests.TryGetValue(requestHash, out OAuthAuthorizationRequest? request) || request.CompletedAt != null)
                {
                    return Task.FromResult(false);
                }

                request.CompletedAt = completedAt;
                return Task.FromResult(true);
            }
        }

        /// <inheritdoc />
        public Task StoreAuthorizationCodeAsync(OAuthAuthorizationCode code, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(code);
            lock (_lock)
            {
                _codes.Add(code.CodeHash, Copy(code));
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<OAuthAuthorizationCode?> GetAuthorizationCodeAsync(string codeHash, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult(_codes.TryGetValue(codeHash, out OAuthAuthorizationCode? code) ? Copy(code) : null);
            }
        }

        /// <inheritdoc />
        public Task<bool> RedeemAuthorizationCodeAsync(string codeHash, DateTime redeemedAt, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (!_codes.TryGetValue(codeHash, out OAuthAuthorizationCode? code) || code.RedeemedAt != null)
                {
                    return Task.FromResult(false);
                }

                code.RedeemedAt = redeemedAt;
                return Task.FromResult(true);
            }
        }

        /// <inheritdoc />
        public Task StoreRefreshTokenAsync(OAuthRefreshToken refreshToken, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(refreshToken);
            lock (_lock)
            {
                _refreshTokens.Add(refreshToken.TokenHash, Copy(refreshToken));
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<OAuthRefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult(_refreshTokens.TryGetValue(tokenHash, out OAuthRefreshToken? token) ? Copy(token) : null);
            }
        }

        /// <inheritdoc />
        public Task<bool> ConsumeRefreshTokenAsync(string tokenHash, DateTime consumedAt, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (!_refreshTokens.TryGetValue(tokenHash, out OAuthRefreshToken? token) || token.ConsumedAt != null)
                {
                    return Task.FromResult(false);
                }

                token.ConsumedAt = consumedAt;
                return Task.FromResult(true);
            }
        }

        /// <inheritdoc />
        public Task InvalidateRefreshTokenFamilyAsync(string familyId, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                foreach (OAuthRefreshToken token in _refreshTokens.Values.Where(token => token.FamilyId == familyId))
                {
                    token.IsInvalidated = true;
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task InvalidateAuthorizationCodeAsync(string codeHash, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_codes.TryGetValue(codeHash, out OAuthAuthorizationCode? code))
                {
                    code.IsInvalidated = true;
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task InvalidateGrantAuthorizationCodesAsync(string grantId, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                foreach (OAuthAuthorizationCode code in _codes.Values.Where(code => code.GrantId == grantId))
                {
                    code.IsInvalidated = true;
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task InvalidateGrantRefreshTokensAsync(string grantId, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                foreach (OAuthRefreshToken token in _refreshTokens.Values.Where(token => token.GrantId == grantId))
                {
                    token.IsInvalidated = true;
                }
            }

            return Task.CompletedTask;
        }

        private static OAuthAuthorizationRequest Copy(OAuthAuthorizationRequest request)
        {
            return new OAuthAuthorizationRequest
            {
                RequestHash = request.RequestHash,
                ClientId = request.ClientId,
                ClientName = request.ClientName,
                ClientUri = request.ClientUri,
                RedirectUri = request.RedirectUri,
                State = request.State,
                CodeChallenge = request.CodeChallenge,
                Scope = request.Scope,
                Resource = request.Resource,
                ExpiresAt = request.ExpiresAt,
                CompletedAt = request.CompletedAt,
            };
        }

        private static OAuthAuthorizationCode Copy(OAuthAuthorizationCode code)
        {
            return new OAuthAuthorizationCode
            {
                CodeHash = code.CodeHash,
                ClientId = code.ClientId,
                RedirectUri = code.RedirectUri,
                CodeChallenge = code.CodeChallenge,
                Resource = code.Resource,
                GrantId = code.GrantId,
                Subject = code.Subject,
                AuthType = code.AuthType,
                SerializedClaims = code.SerializedClaims,
                SerializedRoles = code.SerializedRoles,
                FamilyId = code.FamilyId,
                ExpiresAt = code.ExpiresAt,
                RedeemedAt = code.RedeemedAt,
                IsInvalidated = code.IsInvalidated,
            };
        }

        private static OAuthRefreshToken Copy(OAuthRefreshToken token)
        {
            return new OAuthRefreshToken
            {
                TokenHash = token.TokenHash,
                FamilyId = token.FamilyId,
                GrantId = token.GrantId,
                ClientId = token.ClientId,
                Subject = token.Subject,
                AuthType = token.AuthType,
                SerializedClaims = token.SerializedClaims,
                SerializedRoles = token.SerializedRoles,
                CreatedAt = token.CreatedAt,
                ExpiresAt = token.ExpiresAt,
                ConsumedAt = token.ConsumedAt,
                IsInvalidated = token.IsInvalidated,
            };
        }
    }
}
