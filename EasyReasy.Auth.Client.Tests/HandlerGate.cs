namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// Holds one request in <see cref="FakeHttpHandler"/> until the test releases it, so a test can start other
    /// calls while that request is in flight.
    /// </summary>
    public class HandlerGate
    {
        private readonly TaskCompletionSource _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Completes when the gated request reaches the handler.
        /// </summary>
        public Task Entered => _entered.Task;

        /// <summary>
        /// Lets the gated request's answer through.
        /// </summary>
        public void Release()
        {
            _released.TrySetResult();
        }

        /// <summary>
        /// Called by the handler: marks the request as entered and waits for <see cref="Release"/>, honouring the
        /// request's cancellation token the way a real request does.
        /// </summary>
        /// <param name="cancellationToken">The request's cancellation token.</param>
        internal async Task PassAsync(CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            await _released.Task.WaitAsync(cancellationToken);
        }
    }
}
