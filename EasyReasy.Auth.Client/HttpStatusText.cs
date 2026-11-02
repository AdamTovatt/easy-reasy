using System.Net;

namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// Writes an HTTP status the one way this library's messages show it, so log lines from different failures
    /// read alike.
    /// </summary>
    internal static class HttpStatusText
    {
        /// <summary>
        /// Writes <paramref name="statusCode"/> as its number and name, such as <c>404 NotFound</c>.
        /// </summary>
        /// <param name="statusCode">The status to write.</param>
        /// <returns>The status as text.</returns>
        public static string Describe(HttpStatusCode statusCode)
        {
            return $"{(int)statusCode} {statusCode}";
        }
    }
}
