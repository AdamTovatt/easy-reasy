namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Configurable <see cref="IRefreshClaimsResolver"/> for testing. Records every invocation
    /// so tests can assert what context was passed, and can be configured to allow with
    /// specific claims/roles, deny, or throw.
    /// </summary>
    internal sealed class FakeRefreshClaimsResolver : IRefreshClaimsResolver
    {
        private readonly Func<RefreshClaimsContext, RefreshClaimsDecision> _decide;

        public List<RefreshClaimsContext> Calls { get; } = new List<RefreshClaimsContext>();
        public List<CancellationToken> ReceivedCancellationTokens { get; } = new List<CancellationToken>();

        public FakeRefreshClaimsResolver(Func<RefreshClaimsContext, RefreshClaimsDecision> decide)
        {
            _decide = decide;
        }

        public Task<RefreshClaimsDecision> ResolveAsync(RefreshClaimsContext context, CancellationToken cancellationToken)
        {
            Calls.Add(context);
            ReceivedCancellationTokens.Add(cancellationToken);
            try
            {
                RefreshClaimsDecision decision = _decide(context);
                return Task.FromResult(decision);
            }
            catch (Exception ex)
            {
                return Task.FromException<RefreshClaimsDecision>(ex);
            }
        }
    }
}
