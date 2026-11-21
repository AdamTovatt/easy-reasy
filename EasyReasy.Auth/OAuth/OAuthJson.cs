using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The fixed JSON settings the authorization server writes its responses with. The wire names are the RFCs' own and
    /// spelled out on each model, so the host's naming policy or <see cref="JsonSerializerSettings.CurrentOptions"/> never
    /// changes them.
    /// </summary>
    internal static class OAuthJson
    {
        /// <summary>
        /// The settings: no naming policy, nulls left out.
        /// </summary>
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };

        /// <summary>
        /// The most bytes of a JSON request body or fetched document the server reads.
        /// </summary>
        public const int MaxRequestBodyBytes = 16 * 1024;

        /// <summary>
        /// Reads a request or response body as JSON, up to a byte limit.
        /// </summary>
        /// <param name="body">The body stream.</param>
        /// <param name="maxBytes">The most bytes to read.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The parsed document, or null when the body is empty, too large or not JSON.</returns>
        public static async Task<JsonDocument?> TryReadAsync(Stream body, int maxBytes, CancellationToken cancellationToken)
        {
            ReadOnlyMemory<byte>? bytes = await ReadLimitedAsync(body, maxBytes, cancellationToken);
            return bytes == null || bytes.Value.Length == 0 ? null : TryParse(bytes.Value);
        }

        /// <summary>
        /// Reads a stream to its end, up to a byte limit.
        /// </summary>
        /// <param name="body">The stream.</param>
        /// <param name="maxBytes">The most bytes to read.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The bytes, possibly none, or null when the stream holds more than the limit.</returns>
        public static async Task<ReadOnlyMemory<byte>?> ReadLimitedAsync(Stream body, int maxBytes, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[maxBytes + 1];
            int total = 0;
            while (total < buffer.Length)
            {
                int read = await body.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            // Two statements, not a conditional: in "total > maxBytes ? null : buffer.AsMemory(...)" the null converts to
            // an empty Memory<byte> through the array conversion, and an oversized body would read as no body.
            if (total > maxBytes)
            {
                return null;
            }

            return new ReadOnlyMemory<byte>(buffer, 0, total);
        }

        /// <summary>
        /// Parses JSON.
        /// </summary>
        /// <param name="bytes">The UTF-8 JSON.</param>
        /// <returns>The document, or null when it is not JSON.</returns>
        public static JsonDocument? TryParse(ReadOnlyMemory<byte> bytes)
        {
            try
            {
                return JsonDocument.Parse(bytes);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
