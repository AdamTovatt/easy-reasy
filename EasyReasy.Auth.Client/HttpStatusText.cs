using System.Net;

namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// Writes an HTTP status the one way this library's messages show it, so log lines from different failures
    /// read alike.
    /// </summary>
    /// <remarks>
    /// Public so that code reporting statuses next to this library's messages words them the same way rather than
    /// keeping a copy of the format.
    /// </remarks>
    public static class HttpStatusText
    {
        /// <summary>
        /// Writes <paramref name="statusCode"/> as its number and name, such as <c>404 NotFound</c>.
        /// </summary>
        /// <remarks>
        /// The name is the <see cref="HttpStatusCode"/> member's name. A status with no member, such as
        /// <c>499</c>, has no name to write and is written as its number alone: <c>499</c>.
        /// </remarks>
        /// <param name="statusCode">The status to write.</param>
        /// <returns>The status as text.</returns>
        public static string Describe(HttpStatusCode statusCode)
        {
            return Enum.IsDefined(statusCode) ? $"{(int)statusCode} {statusCode}" : $"{(int)statusCode}";
        }
    }
}
