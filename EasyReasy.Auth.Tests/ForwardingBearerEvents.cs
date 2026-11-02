using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A consumer's own wrapper that forwards every event to the events it wraps, as an app might put around the
    /// library's wrapper in a later <c>PostConfigure</c>. The library cannot verify what it forwards, so startup
    /// must reject it.
    /// </summary>
    internal sealed class ForwardingBearerEvents : JwtBearerEvents
    {
        private readonly JwtBearerEvents _inner;

        public ForwardingBearerEvents(JwtBearerEvents inner)
        {
            _inner = inner;
        }

        public override Task MessageReceived(MessageReceivedContext context) => _inner.MessageReceived(context);

        public override Task AuthenticationFailed(AuthenticationFailedContext context) => _inner.AuthenticationFailed(context);

        public override Task TokenValidated(TokenValidatedContext context) => _inner.TokenValidated(context);

        public override Task Challenge(JwtBearerChallengeContext context) => _inner.Challenge(context);

        public override Task Forbidden(ForbiddenContext context) => _inner.Forbidden(context);
    }
}
