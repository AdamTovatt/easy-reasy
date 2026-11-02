using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Lets a request on a configured path carry its bearer token in the <c>access_token</c> query-string
    /// parameter, for the WebSocket connections a browser cannot attach an <c>Authorization</c> header to.
    /// </summary>
    internal sealed class QueryStringTokenFallback : IBearerTokenSource
    {
        internal const string ParameterName = "access_token";

        private readonly PathPrefixList _paths;

        internal QueryStringTokenFallback(IEnumerable<string> paths)
        {
            _paths = new PathPrefixList(paths);
        }

        /// <inheritdoc />
        public string OptionName => nameof(EasyReasyAuthOptions.QueryStringTokenPaths);

        /// <summary>
        /// Returns the query-string token of a request on a configured path, or <c>null</c> when the request does
        /// not qualify.
        /// </summary>
        public string? ReadToken(HttpRequest request)
        {
            if (!_paths.Matches(request.Path))
            {
                return null;
            }

            StringValues values = request.Query[ParameterName];

            // A repeated parameter is ambiguous about which token the client meant, so neither is used. An empty
            // value needs no check of its own: the events wrapper treats an empty token as no token.
            return values.Count == 1 ? values[0] : null;
        }
    }
}
