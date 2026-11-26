using System.Collections.Concurrent;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// An <see cref="IOpenIdConnectProviderResolver"/> over a table a test changes while flows are in progress, to add,
    /// change or remove a provider as an application can without a restart. Records each key it was asked for.
    /// </summary>
    internal sealed class FakeProviderResolver : IOpenIdConnectProviderResolver
    {
        public ConcurrentDictionary<string, OpenIdConnectProvider> Providers { get; } = new ConcurrentDictionary<string, OpenIdConnectProvider>(StringComparer.Ordinal);

        public ConcurrentQueue<string> RequestedKeys { get; } = new ConcurrentQueue<string>();

        public Task<OpenIdConnectProvider?> FindProviderAsync(string providerKey, CancellationToken cancellationToken)
        {
            RequestedKeys.Enqueue(providerKey);
            return Task.FromResult(Providers.TryGetValue(providerKey, out OpenIdConnectProvider? provider) ? provider : null);
        }
    }
}
