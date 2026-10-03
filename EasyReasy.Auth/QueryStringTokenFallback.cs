using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Lets a request on a configured path carry its bearer token in the <c>access_token</c> query-string
    /// parameter, for the WebSocket connections a browser cannot attach an <c>Authorization</c> header to.
    /// </summary>
    internal sealed class QueryStringTokenFallback
    {
        internal const string ParameterName = "access_token";

        private readonly PathString[] _paths;

        internal QueryStringTokenFallback(IEnumerable<string> paths)
        {
            _paths = paths.Select(path => new PathString(path)).ToArray();
        }

        /// <summary>
        /// Replaces the events on <paramref name="bearerOptions"/> with a wrapper around them, so a consumer who
        /// assigned their own events keeps every handler and gains the fallback.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="AuthenticationSchemeOptions.EventsType"/> is set, since the handler then resolves its events
        /// from DI and never calls the ones installed here.
        /// </exception>
        internal void Install(JwtBearerOptions bearerOptions)
        {
            if (bearerOptions.EventsType != null)
            {
                throw new InvalidOperationException(
                    $"{nameof(EasyReasyAuthOptions.QueryStringTokenPaths)} cannot be combined with {nameof(JwtBearerOptions)}.{nameof(JwtBearerOptions.EventsType)}, " +
                    $"because the bearer handler then ignores {nameof(JwtBearerOptions)}.{nameof(JwtBearerOptions.Events)}, where the query-string fallback is installed. " +
                    $"Assign {nameof(JwtBearerOptions)}.{nameof(JwtBearerOptions.Events)} instead; the fallback wraps it.");
            }

            bearerOptions.Events = new QueryStringTokenBearerEvents(bearerOptions.Events ?? new JwtBearerEvents(), this);
        }

        /// <summary>
        /// Returns the query-string token of a request on a configured path that carries no <c>Authorization</c>
        /// header, or <c>null</c> when the request does not qualify.
        /// </summary>
        internal string? ReadToken(HttpRequest request)
        {
            if (request.Headers.ContainsKey(HeaderNames.Authorization))
            {
                return null;
            }

            if (!_paths.Any(path => request.Path.StartsWithSegments(path)))
            {
                return null;
            }

            StringValues values = request.Query[ParameterName];

            // A repeated parameter is ambiguous about which token the client meant, so neither is used. An empty
            // value needs no check of its own: the handler treats an empty token as no token.
            return values.Count == 1 ? values[0] : null;
        }
    }
}
