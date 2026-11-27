using System.Collections.Concurrent;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// An <see cref="ISamlAuthStore"/> in memory, atomic as the interface requires: each consume removes the record, so
    /// only its first caller gets it.
    /// </summary>
    internal sealed class InMemorySamlAuthStore : ISamlAuthStore
    {
        private readonly ConcurrentDictionary<string, SamlPendingRequestRecord> _pendingRequests = new ConcurrentDictionary<string, SamlPendingRequestRecord>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, SamlCompletionRecord> _completions = new ConcurrentDictionary<string, SamlCompletionRecord>(StringComparer.Ordinal);

        public IReadOnlyCollection<SamlPendingRequestRecord> PendingRequests => _pendingRequests.Values.ToList();

        public IReadOnlyCollection<SamlCompletionRecord> Completions => _completions.Values.ToList();

        public Task StorePendingRequestAsync(SamlPendingRequestRecord record, CancellationToken cancellationToken = default)
        {
            _pendingRequests[record.RequestIdHash] = record;
            return Task.CompletedTask;
        }

        public Task<string?> ConsumePendingRequestAsync(string requestIdHash, DateTime now, CancellationToken cancellationToken = default)
        {
            bool removed = _pendingRequests.TryRemove(requestIdHash, out SamlPendingRequestRecord? record);
            return Task.FromResult(removed && record!.ExpiresAt > now ? record.SealedState : null);
        }

        public Task StoreCompletionAsync(SamlCompletionRecord record, CancellationToken cancellationToken = default)
        {
            _completions[record.CodeHash] = record;
            return Task.CompletedTask;
        }

        public Task<string?> ConsumeCompletionAsync(string codeHash, DateTime now, CancellationToken cancellationToken = default)
        {
            bool removed = _completions.TryRemove(codeHash, out SamlCompletionRecord? record);
            return Task.FromResult(removed && record!.ExpiresAt > now ? record.SealedOutcome : null);
        }

        public Task DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken = default)
        {
            foreach (KeyValuePair<string, SamlPendingRequestRecord> pair in _pendingRequests.Where(pair => pair.Value.ExpiresAt <= now))
            {
                _pendingRequests.TryRemove(pair.Key, out _);
            }

            foreach (KeyValuePair<string, SamlCompletionRecord> pair in _completions.Where(pair => pair.Value.ExpiresAt <= now))
            {
                _completions.TryRemove(pair.Key, out _);
            }

            return Task.CompletedTask;
        }
    }
}
