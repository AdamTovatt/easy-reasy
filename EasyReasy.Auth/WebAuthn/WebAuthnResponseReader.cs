using System.Text;
using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Reads a credential the browser sent back — the document and its fields — reporting anything that is
    /// not a credential as an <see cref="ArgumentException"/>.
    /// </summary>
    /// <remarks>
    /// The reading itself is <see cref="JsonFieldReader"/>'s; what this type fixes is the failure those
    /// reads mean here, and the line between the two kinds of failure. A body missing <c>clientDataJSON</c>,
    /// or carrying a number where a string belongs, is not a WebAuthn credential at all — it is a malformed
    /// request, and the endpoint answers it the way it answers any other malformed body. Everything a real
    /// ceremony can produce and still fail on is a result object instead, so an application never has to
    /// catch an exception to learn that a security key was declined.
    /// </remarks>
    internal static class WebAuthnResponseReader
    {
        /// <summary>
        /// Longest response body this library will parse, in bytes of UTF-8.
        /// </summary>
        /// <remarks>
        /// The body is untrusted and arrives before anything has decided it is a credential, so it is
        /// bounded before it is read rather than after. Generous against the largest real response — an
        /// RS256 credential's attestation object runs to a couple of kilobytes once base64url-encoded —
        /// and the bound is here, on the whole body, rather than repeated on each field: a field cannot
        /// outgrow the document it came from, so one check covers all of them.
        /// </remarks>
        public const int MaximumResponseLength = 64 * 1024;

        /// <summary>
        /// Longest base64url field this library will decode, bounding the fields reached through a
        /// constructor rather than through <see cref="ParseObject"/>.
        /// </summary>
        public const int MaximumEncodedFieldLength = 16 * 1024;

        private static readonly Func<string, Exception> MalformedResponse = message => new ArgumentException(message);

        /// <summary>
        /// Decodes a required base64url field.
        /// </summary>
        /// <param name="value">The encoded text.</param>
        /// <param name="parameterName">The parameter to name in the exception.</param>
        /// <returns>The decoded bytes.</returns>
        /// <exception cref="ArgumentException">The text is too long, is not base64url, or stands for no bytes.</exception>
        public static byte[] Decode(string value, string parameterName)
        {
            if (value.Length > MaximumEncodedFieldLength)
            {
                throw new ArgumentException(
                    $"'{parameterName}' is {value.Length} characters; no field of a credential is longer than {MaximumEncodedFieldLength}.",
                    parameterName);
            }

            byte[]? decoded = Base64UrlEncoding.TryDecode(value);
            if (decoded == null)
            {
                throw new ArgumentException($"'{parameterName}' is not base64url; the browser's JSON contract encodes it that way.", parameterName);
            }

            if (decoded.Length == 0)
            {
                // Covers the blank field as well as the empty one: whitespace is valid base64url and
                // decodes to nothing, so a separate emptiness check on the text would reject only the
                // narrower of the two cases this already rejects.
                throw new ArgumentException($"'{parameterName}' stands for no bytes; it must carry the base64url of a value.", parameterName);
            }

            return decoded;
        }

        /// <summary>
        /// Decodes an optional base64url field, returning null when it is absent.
        /// </summary>
        /// <remarks>
        /// Holds an optional field to the same rules as a required one when it is present. The field is
        /// untrusted and is documented to applications as base64url they may decode, so leaving one field
        /// of the envelope unbounded would undo on that field what bounding the envelope does for the rest
        /// — and would hand an application a value this library called base64url without checking.
        /// </remarks>
        /// <param name="value">The encoded text, or null.</param>
        /// <param name="parameterName">The parameter to name in the exception.</param>
        /// <returns>The decoded bytes, or null when the field is absent.</returns>
        /// <exception cref="ArgumentException">The text is present and is too long, is not base64url, or stands for no bytes.</exception>
        public static byte[]? DecodeOptional(string? value, string parameterName)
        {
            return value == null ? null : Decode(value, parameterName);
        }

        /// <summary>
        /// Reads a required, non-empty string property.
        /// </summary>
        /// <param name="element">The JSON object to read from.</param>
        /// <param name="propertyName">The property to read.</param>
        /// <param name="what">The structure being read, named as it reads in a message.</param>
        /// <returns>The property's value.</returns>
        /// <exception cref="ArgumentException">The property is absent, is not a string, or is empty.</exception>
        public static string ReadRequiredString(JsonElement element, string propertyName, string what)
        {
            return JsonFieldReader.ReadRequiredString(element, propertyName, what, MalformedResponse);
        }

        /// <summary>
        /// Reads an optional string property, returning null when it is absent or null.
        /// </summary>
        /// <param name="element">The JSON object to read from.</param>
        /// <param name="propertyName">The property to read.</param>
        /// <param name="what">The structure being read, named as it reads in a message.</param>
        /// <returns>The property's value, or null.</returns>
        /// <exception cref="ArgumentException">The property is present and is not a string.</exception>
        public static string? ReadOptionalString(JsonElement element, string propertyName, string what)
        {
            return JsonFieldReader.ReadOptionalString(element, propertyName, what, MalformedResponse);
        }

        /// <summary>
        /// Reads an optional array of strings, returning null when it is absent or null.
        /// </summary>
        /// <param name="element">The JSON object to read from.</param>
        /// <param name="propertyName">The property to read.</param>
        /// <param name="what">The structure being read, named as it reads in a message.</param>
        /// <returns>The property's values, or null.</returns>
        /// <exception cref="ArgumentException">The property is present and is not an array of strings.</exception>
        public static IReadOnlyList<string>? ReadOptionalStringArray(JsonElement element, string propertyName, string what)
        {
            return JsonFieldReader.ReadOptionalStringArray(element, propertyName, what, MalformedResponse);
        }

        /// <summary>
        /// Reads a required object property.
        /// </summary>
        /// <param name="element">The JSON object to read from.</param>
        /// <param name="propertyName">The property to read.</param>
        /// <param name="what">The structure being read, named as it reads in a message.</param>
        /// <returns>The property's value.</returns>
        /// <exception cref="ArgumentException">The property is absent or is not an object.</exception>
        public static JsonElement ReadRequiredObject(JsonElement element, string propertyName, string what)
        {
            return JsonFieldReader.ReadRequiredObject(element, propertyName, what, MalformedResponse);
        }

        /// <summary>
        /// Parses a browser response body into its root JSON object.
        /// </summary>
        /// <param name="json">The JSON text.</param>
        /// <param name="what">The structure being read, named as it reads in a message.</param>
        /// <returns>The parsed document, which the caller owns and must dispose.</returns>
        /// <exception cref="ArgumentException">The text is too long, or is not a JSON object.</exception>
        public static JsonDocument ParseObject(string json, string what)
        {
            // Counted in UTF-8 bytes, as the converter path counts the bytes it is handed, so one rule bounds both.
            EnsureWithinBound(Encoding.UTF8.GetByteCount(json), what);

            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new ArgumentException($"{what} is not valid JSON: {exception.Message}");
            }

            return EnsureObject(document, what);
        }

        /// <summary>
        /// Reads a browser response nested in a larger document, from a System.Text.Json converter, by the same
        /// rules as <see cref="ParseObject"/>.
        /// </summary>
        /// <remarks>
        /// A value that is not a credential becomes a <see cref="JsonException"/> carrying the message
        /// <see cref="ParseObject"/> and <paramref name="read"/> would give it as an <see cref="ArgumentException"/>,
        /// so ASP.NET model binding answers it as a malformed body (400) rather than a server error.
        /// </remarks>
        /// <typeparam name="T">The response type.</typeparam>
        /// <param name="reader">The reader, on the first token of the value; left on its last.</param>
        /// <param name="what">The structure being read, named as it reads in a message.</param>
        /// <param name="read">Reads the response from its root JSON object.</param>
        /// <returns>The response.</returns>
        /// <exception cref="JsonException">The value is too long, is not a JSON object, or is not a credential.</exception>
        public static T ReadNested<T>(ref Utf8JsonReader reader, string what, Func<JsonElement, T> read)
        {
            try
            {
                // Measured on a copy of the reader, so the bound holds before any document is built from the value.
                // TrySkip rather than Skip: a body read from a stream reaches the converter on a reader that is not
                // the final block, where Skip throws even though the serializer has buffered the whole value.
                Utf8JsonReader measuringReader = reader;
                long valueStart = measuringReader.TokenStartIndex;
                if (!measuringReader.TrySkip())
                {
                    // The serializer buffers a converter's whole value before calling it, so this is not reached through it.
                    throw new InvalidOperationException($"{what} is not wholly buffered; read it through JsonSerializer.");
                }

                EnsureWithinBound(measuringReader.BytesConsumed - valueStart, what);

                using JsonDocument document = EnsureObject(JsonDocument.ParseValue(ref reader), what);
                return read(document.RootElement);
            }
            catch (ArgumentException exception)
            {
                throw new JsonException(exception.Message, exception);
            }
        }

        private static void EnsureWithinBound(long utf8ByteCount, string what)
        {
            if (utf8ByteCount > MaximumResponseLength)
            {
                throw new ArgumentException($"{what} is {utf8ByteCount} bytes of UTF-8; no credential is longer than {MaximumResponseLength}.");
            }
        }

        private static JsonDocument EnsureObject(JsonDocument document, string what)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                throw new ArgumentException($"{what} is not a JSON object.");
            }

            return document;
        }
    }
}
