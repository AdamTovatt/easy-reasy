using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Bearer events that delegate every event to the consumer's own <see cref="JwtBearerEvents"/>, then to any
    /// <c>On...</c> handler set on this wrapper after it was installed, and, after <see cref="MessageReceived"/>, fill a
    /// token neither left set from the first <see cref="IBearerTokenSource"/> that holds one: the query string, then
    /// the cookie.
    /// </summary>
    /// <remarks>
    /// Wrapping rather than mutating the consumer's instance keeps a shared instance untouched when the options
    /// are rebuilt, and calling the virtual methods rather than the <c>On...</c> delegates keeps a consumer's
    /// subclass overrides working. A consumer's <c>PostConfigure</c> registered after <c>AddEasyReasyAuth</c> sees
    /// this wrapper as <see cref="JwtBearerOptions.Events"/>, so each override also runs the wrapper's own delegate
    /// through <c>base</c>, but only while the inner events have left the outcome open: no result set, or for
    /// <see cref="Challenge"/> no handled response, so a later handler never overwrites a decided result or writes a
    /// response twice. The bearer handler stops at a decided outcome in every event but <see cref="Forbidden"/>, which
    /// it never reads a result from; the gate there is this library's own rule, kept so one rule holds for every event.
    /// Every virtual method must be overridden here and take both paths, or that event would
    /// stop reaching the consumer; a test enumerates them against the events the forwarding tests cover.
    /// <see cref="FallbackTokenBearerEventsValidator"/> fails startup if a later <c>PostConfigure</c> replaces this
    /// wrapper.
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
        /// Turns the token fallback on for the bearer scheme: installs the wrapper around the consumer's events after
        /// every <c>Configure</c>, and fails startup through <see cref="FallbackTokenBearerEventsValidator"/> if the
        /// final options no longer reach it. One wrapper serves every source, so the consumer's events are wrapped once.
        /// </summary>
        /// <param name="services">The service collection the bearer scheme is registered in.</param>
        /// <param name="sources">The token sources, in precedence order.</param>
        internal static void AddTo(IServiceCollection services, IReadOnlyList<IBearerTokenSource> sources)
        {
            // Post-configure runs after every Configure, so it wraps a consumer's own events rather than being
            // replaced by them. A consumer's PostConfigure registered later runs after it, so only validation, which
            // sees the final options, can tell that it replaced the wrapper; on start, not on the first request.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, bearerOptions => Install(bearerOptions, sources));
            services.AddSingleton<IValidateOptions<JwtBearerOptions>>(new FallbackTokenBearerEventsValidator(sources));
            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).ValidateOnStart();
        }

        /// <summary>
        /// Replaces the events on <paramref name="bearerOptions"/> with a wrapper around them that consults
        /// <paramref name="sources"/> in order, so a consumer who assigned their own events keeps every handler and
        /// gains the fallbacks. An <see cref="AuthenticationSchemeOptions.EventsType"/> that makes the handler ignore
        /// the wrapper is the validator's to report, wherever it was set.
        /// </summary>
        private static void Install(JwtBearerOptions bearerOptions, IReadOnlyList<IBearerTokenSource> sources)
        {
            bearerOptions.Events = new FallbackTokenBearerEvents(bearerOptions.Events ?? new JwtBearerEvents(), sources.ToArray());
        }

        /// <inheritdoc />
        public override async Task MessageReceived(MessageReceivedContext context)
        {
            await _inner.MessageReceived(context);

            if (context.Result == null)
            {
                await base.MessageReceived(context);
            }

            // A result needs no token: the handler returns it without reading one.
            // Any Authorization header, Bearer or not, is the client's explicit choice and beats every fallback.
            if (context.Result != null || context.Token != null || context.Request.Headers.ContainsKey(HeaderNames.Authorization))
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
        public override async Task AuthenticationFailed(AuthenticationFailedContext context)
        {
            await _inner.AuthenticationFailed(context);

            if (context.Result == null)
            {
                await base.AuthenticationFailed(context);
            }
        }

        /// <inheritdoc />
        public override async Task TokenValidated(TokenValidatedContext context)
        {
            await _inner.TokenValidated(context);

            if (context.Result == null)
            {
                await base.TokenValidated(context);
            }
        }

        /// <inheritdoc />
        public override async Task Challenge(JwtBearerChallengeContext context)
        {
            await _inner.Challenge(context);

            // A handled challenge has written its response; a second handler must not write another.
            if (!context.Handled)
            {
                await base.Challenge(context);
            }
        }

        /// <inheritdoc />
        public override async Task Forbidden(ForbiddenContext context)
        {
            await _inner.Forbidden(context);

            if (context.Result == null)
            {
                await base.Forbidden(context);
            }
        }
    }
}
