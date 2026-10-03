using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Bearer events that delegate every event to the consumer's own <see cref="JwtBearerEvents"/> and, after
    /// <see cref="MessageReceived"/>, let <see cref="QueryStringTokenFallback"/> fill a token the consumer left unset.
    /// </summary>
    /// <remarks>
    /// Wrapping rather than mutating the consumer's instance keeps a shared instance untouched when the options
    /// are rebuilt, and calling the virtual methods rather than the <c>On...</c> delegates keeps a consumer's
    /// subclass overrides working. Every virtual method must be overridden here, or that event would stop
    /// reaching the consumer; a test enumerates them.
    /// </remarks>
    internal sealed class QueryStringTokenBearerEvents : JwtBearerEvents
    {
        private readonly JwtBearerEvents _inner;
        private readonly QueryStringTokenFallback _fallback;

        internal QueryStringTokenBearerEvents(JwtBearerEvents inner, QueryStringTokenFallback fallback)
        {
            _inner = inner;
            _fallback = fallback;
        }

        /// <inheritdoc />
        public override async Task MessageReceived(MessageReceivedContext context)
        {
            await _inner.MessageReceived(context);

            // A result the consumer set needs no check here: the handler returns it without reading the token.
            if (context.Token == null)
            {
                context.Token = _fallback.ReadToken(context.Request);
            }
        }

        /// <inheritdoc />
        public override Task AuthenticationFailed(AuthenticationFailedContext context) => _inner.AuthenticationFailed(context);

        /// <inheritdoc />
        public override Task TokenValidated(TokenValidatedContext context) => _inner.TokenValidated(context);

        /// <inheritdoc />
        public override Task Challenge(JwtBearerChallengeContext context) => _inner.Challenge(context);

        /// <inheritdoc />
        public override Task Forbidden(ForbiddenContext context) => _inner.Forbidden(context);
    }
}
