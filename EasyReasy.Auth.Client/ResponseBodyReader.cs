using System.Buffers;
using System.Text;

namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// Reads an auth endpoint's response body, under a cap and in whatever encoding the response declared.
    /// </summary>
    internal static class ResponseBodyReader
    {
        /// <summary>
        /// The most characters of a body this reads.
        /// </summary>
        /// <remarks>
        /// An <see cref="AuthResponse"/> is a token, an expiry and a refresh token; nothing near this size is one.
        /// The cap is here because the body comes from whatever address the client was pointed at, and a wrong one
        /// is under no obligation to keep it small: unbounded, it would be held in memory in full, redacted in full
        /// and built into a message in full, all on the word of the host being diagnosed. A body that runs past the
        /// cap is read only that far and reported as what it is — a body that is not an auth response.
        /// </remarks>
        internal const int MaximumCharacters = 64 * 1024;

        /// <summary>
        /// Reads at most <see cref="MaximumCharacters"/> characters of a response body.
        /// </summary>
        /// <remarks>
        /// The buffer is rented rather than allocated: at this size every read would otherwise put an array on the
        /// large object heap, and a long-running client refreshing on a timer does this on every refresh.
        /// </remarks>
        /// <param name="response">The response to read.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The body, up to the cap.</returns>
        internal static async Task<string> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            using StreamReader reader = new StreamReader(
                stream,
                ResolveEncoding(response.Content.Headers.ContentType?.CharSet),
                detectEncodingFromByteOrderMarks: true);

            char[] buffer = ArrayPool<char>.Shared.Rent(MaximumCharacters);

            try
            {
                int read = await reader.ReadBlockAsync(buffer.AsMemory(0, MaximumCharacters), cancellationToken);

                return new string(buffer, 0, read);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Resolves the encoding a body is read with from the content type's charset.
        /// </summary>
        /// <param name="charSet">The charset the response declared, or <c>null</c> when it declared none.</param>
        /// <returns>The encoding to read with, defaulting to UTF-8.</returns>
        private static Encoding ResolveEncoding(string? charSet)
        {
            if (string.IsNullOrWhiteSpace(charSet))
            {
                return Encoding.UTF8;
            }

            try
            {
                return Encoding.GetEncoding(charSet.Trim('"'));
            }
            catch (ArgumentException)
            {
                // A charset no encoding answers to is one more thing an address that is not an auth server can
                // send, and it is not worth failing the diagnosis over: UTF-8 reads the body well enough to report
                // what it was.
                return Encoding.UTF8;
            }
        }
    }
}
