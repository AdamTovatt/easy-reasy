using System.Collections.Concurrent;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// An <see cref="ISamlAuthStore"/> in memory, atomic as the interface requires: each consume removes the record, so
    /// only its first caller gets it.
    /// </summary>
    internal sealed class InMemorySamlAuthStore : ISamlAuthStore
    {
        private readonly ConcurrentDictionary<(SamlAuthRecordKind Kind, string Hash), SamlAuthRecord> _records = new ConcurrentDictionary<(SamlAuthRecordKind Kind, string Hash), SamlAuthRecord>();

        public IReadOnlyCollection<SamlAuthRecord> PendingRequests => RecordsOf(SamlAuthRecordKind.PendingRequest);

        public IReadOnlyCollection<SamlAuthRecord> Completions => RecordsOf(SamlAuthRecordKind.Completion);

        public Task StoreAsync(SamlAuthRecord record, CancellationToken cancellationToken = default)
        {
            _records[(record.Kind, record.Hash)] = record;
            return Task.CompletedTask;
        }

        public Task<string?> ConsumeAsync(SamlAuthRecordKind kind, string hash, DateTime now, CancellationToken cancellationToken = default)
        {
            bool removed = _records.TryRemove((kind, hash), out SamlAuthRecord? record);
            return Task.FromResult(removed && record!.ExpiresAt > now ? record.SealedValue : null);
        }

        public Task DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken = default)
        {
            foreach (KeyValuePair<(SamlAuthRecordKind Kind, string Hash), SamlAuthRecord> pair in _records.Where(pair => pair.Value.ExpiresAt <= now))
            {
                _records.TryRemove(pair.Key, out _);
            }

            return Task.CompletedTask;
        }

        private IReadOnlyCollection<SamlAuthRecord> RecordsOf(SamlAuthRecordKind kind)
        {
            return _records.Values.Where(record => record.Kind == kind).ToList();
        }
    }
}
