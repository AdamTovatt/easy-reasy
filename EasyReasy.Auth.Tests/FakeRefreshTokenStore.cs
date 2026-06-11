namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// In-memory implementation of <see cref="IRefreshTokenStore"/> for testing purposes.
    /// </summary>
    public class FakeRefreshTokenStore : IRefreshTokenStore
    {
        private readonly Dictionary<string, StoredRefreshToken> _tokens = new Dictionary<string, StoredRefreshToken>();
        private readonly List<string> _invalidatedFamilyIds = new List<string>();

        public Task StoreAsync(StoredRefreshToken refreshToken, CancellationToken cancellationToken = default)
        {
            _tokens[refreshToken.TokenHash] = refreshToken;
            return Task.CompletedTask;
        }

        public Task<StoredRefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            _tokens.TryGetValue(tokenHash, out StoredRefreshToken? token);
            return Task.FromResult(token);
        }

        public Task<bool> MarkAsConsumedAsync(string tokenHash, DateTime consumedAt, CancellationToken cancellationToken = default)
        {
            if (_tokens.TryGetValue(tokenHash, out StoredRefreshToken? token))
            {
                if (token.ConsumedAt != null)
                {
                    return Task.FromResult(false);
                }

                token.ConsumedAt = consumedAt;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task InvalidateFamilyAsync(string familyId, CancellationToken cancellationToken = default)
        {
            _invalidatedFamilyIds.Add(familyId);
            foreach (StoredRefreshToken token in _tokens.Values)
            {
                if (token.FamilyId == familyId)
                {
                    token.IsInvalidated = true;
                }
            }
            return Task.CompletedTask;
        }

        public Task<int> InvalidateAllFamiliesForUserAsync(string subject, CancellationToken cancellationToken = default)
        {
            HashSet<string> invalidatedFamilies = new HashSet<string>();
            foreach (StoredRefreshToken token in _tokens.Values)
            {
                if (token.Subject == subject && !token.IsInvalidated)
                {
                    token.IsInvalidated = true;
                    invalidatedFamilies.Add(token.FamilyId);
                }
            }
            return Task.FromResult(invalidatedFamilies.Count);
        }

        /// <summary>
        /// Gets all stored tokens for test inspection.
        /// </summary>
        public IReadOnlyDictionary<string, StoredRefreshToken> Tokens => _tokens;

        /// <summary>
        /// Records every family id passed to <see cref="InvalidateFamilyAsync"/>, in call order, so tests can
        /// assert exactly which families were retired and how many times.
        /// </summary>
        public IReadOnlyList<string> InvalidatedFamilyIds => _invalidatedFamilyIds;
    }
}
