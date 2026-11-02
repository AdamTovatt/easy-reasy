using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth
{
    /// <summary>
    /// A place other than the <c>Authorization</c> header that a bearer token may arrive in, consulted by
    /// <see cref="FallbackTokenBearerEvents"/> in a fixed order when the header is absent.
    /// </summary>
    internal interface IBearerTokenSource
    {
        /// <summary>
        /// The name of the <see cref="EasyReasyAuthOptions"/> property that turns this source on, for error messages.
        /// </summary>
        string OptionName { get; }

        /// <summary>
        /// Returns the token this source holds for <paramref name="request"/>, or <c>null</c> when the request does not
        /// qualify. Called only for a request that carries no <c>Authorization</c> header.
        /// </summary>
        string? ReadToken(HttpRequest request);
    }
}
