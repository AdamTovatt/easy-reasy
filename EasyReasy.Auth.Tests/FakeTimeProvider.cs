namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A fake <see cref="TimeProvider"/> whose time only moves when a test moves it.
    /// </summary>
    internal sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset startTime)
        {
            _utcNow = startTime;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
        }

        public void SetUtcNow(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }
    }
}
