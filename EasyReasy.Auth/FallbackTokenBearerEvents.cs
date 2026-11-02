using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Net.Http.Headers;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Bearer events that delegate every event to the consumer's own <see cref="JwtBearerEvents"/> and, after
    /// <see cref="MessageReceived"/>, fill a token the consumer left unset from the first <see cref="IBearerTokenSource"/>
    /// that holds one: the query string, then the cookie.
    /// </summary>
    /// <remarks>
    /// Wrapping rather than mutating the consumer's instance keeps a shared instance untouched when the options
    /// are rebuilt, and calling the virtual methods rather than the <c>On...</c> delegates keeps a consumer's
    /// subclass overrides working. Every virtual method must be overridden here, or that event would stop
    /// reaching the consumer; a test enumerates them.
    /// </remarks>
    internal sealed class FallbackTokenBearerEvents : JwtBearerEvents
    {
        private readonly JwtBearerEvents _inner;
        private readonly IBearerTokenSource[] _sources;

        private FallbackTokenBearerEvents(JwtBearerEvents inner, IBearerTokenSource[] sources)
        {
            _inner = inner;
            _sources = sources;
        }

        /// <summary>
        /// Replaces the events on <paramref name="bearerOptions"/> with a wrapper around them that consults
        /// <paramref name="sources"/> in order, so a consumer who assigned their own events keeps every handler and
        /// gains the fallbacks. One wrapper serves every source, so the consumer's events are wrapped once.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="AuthenticationSchemeOptions.EventsType"/> is set, since the handler then resolves its events
        /// from DI and never calls the ones installed here.
        /// </exception>
        internal static void Install(JwtBearerOptions bearerOptions, IReadOnlyList<IBearerTokenSource> sources)
        {
            if (bearerOptions.EventsType != null)
            {
                string optionNames = string.Join(" and ", sources.Select(source => source.OptionName));

                throw new InvalidOperationException(
                    $"{optionNames} cannot be combined with {nameof(JwtBearerOptions)}.{nameof(JwtBearerOptions.EventsType)}, " +
                    $"because the bearer handler then ignores {nameof(JwtBearerOptions)}.{nameof(JwtBearerOptions.Events)}, where the token fallback is installed. " +
                    $"Assign {nameof(JwtBearerOptions)}.{nameof(JwtBearerOptions.Events)} instead; the fallback wraps it.");
            }

            bearerOptions.Events = new FallbackTokenBearerEvents(bearerOptions.Events ?? new JwtBearerEvents(), sources.ToArray());
        }

        /// <inheritdoc />
        public override async Task MessageReceived(MessageReceivedContext context)
        {
            await _inner.MessageReceived(context);

            // A result the consumer set needs no check here: the handler returns it without reading the token.
            // Any Authorization header, Bearer or not, is the client's explicit choice and beats every fallback.
            if (context.Token != null || context.Request.Headers.ContainsKey(HeaderNames.Authorization))
            {
                return;
            }

            foreach (IBearerTokenSource source in _sources)
            {
                string? token = source.ReadToken(context.Request);

                // An empty value is no token, so it falls through to the next source rather than blocking it.
                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                    return;
                }
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
