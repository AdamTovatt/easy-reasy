using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A consumer's <see cref="JwtBearerEvents"/> subclass that overrides the virtual methods rather than assigning
    /// the <c>On...</c> delegates, counting the calls that reach it.
    /// </summary>
    internal sealed class RecordingBearerEvents : JwtBearerEvents
    {
        private int _messageReceivedCalls;
        private int _tokenValidatedCalls;

        public int MessageReceivedCalls => _messageReceivedCalls;
        public int TokenValidatedCalls => _tokenValidatedCalls;

        public override Task MessageReceived(MessageReceivedContext context)
        {
            Interlocked.Increment(ref _messageReceivedCalls);
            return Task.CompletedTask;
        }

        public override Task TokenValidated(TokenValidatedContext context)
        {
            Interlocked.Increment(ref _tokenValidatedCalls);
            return Task.CompletedTask;
        }
    }
}
