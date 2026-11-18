using System.Buffers;
using System.Text;
using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The one way WebAuthn JSON is written: the options a browser reads and the credentials it posted, through
    /// their <c>ToJson</c> and their System.Text.Json converters alike.
    /// </summary>
    /// <remarks>
    /// The format is WebAuthn's, so nothing an application configures may change it: neither
    /// <see cref="JsonSerializerSettings.CurrentOptions"/>, which the library's own request models use, nor the
    /// <see cref="JsonSerializerOptions"/> of a host document a converter writes into, whose naming policy, null
    /// handling or enum converters would otherwise reach the nested WebAuthn values. Everything here uses fixed
    /// settings of its own.
    /// </remarks>
    internal static class WebAuthnJson
    {
        /// <summary>
        /// The fixed serializer options for the values nested inside a WebAuthn document.
        /// </summary>
        /// <remarks>
        /// No naming policy, on purpose: every member of every WebAuthn type is named with <c>JsonPropertyName</c>,
        /// and one that is not comes out in its C# spelling, which the JSON contract tests catch. Read-only, so
        /// nothing can change it after the fact.
        /// </remarks>
        private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

        /// <summary>
        /// Writes a WebAuthn value as its JSON text.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <returns>The JSON text, exactly the bytes of <see cref="ToUtf8Bytes"/>.</returns>
        public static string ToJson(IWebAuthnJsonWritable value)
        {
            return Encoding.UTF8.GetString(ToUtf8Bytes(value));
        }

        /// <summary>
        /// Writes a WebAuthn value as compact UTF-8 JSON with the default encoder.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <returns>The UTF-8 JSON bytes.</returns>
        public static byte[] ToUtf8Bytes(IWebAuthnJsonWritable value)
        {
            ArrayBufferWriter<byte> buffer = new ArrayBufferWriter<byte>();

            // Default writer options: compact, with the default encoder, whatever the application has configured.
            using (Utf8JsonWriter writer = new Utf8JsonWriter(buffer))
            {
                value.WriteJson(writer);
            }

            return buffer.WrittenSpan.ToArray();
        }

        /// <summary>
        /// Writes one property whose value is serialized with the fixed WebAuthn options.
        /// </summary>
        /// <typeparam name="T">The property's declared type.</typeparam>
        /// <param name="writer">The writer of the enclosing WebAuthn object.</param>
        /// <param name="propertyName">The property's wire name.</param>
        /// <param name="value">The property's value.</param>
        public static void WriteProperty<T>(Utf8JsonWriter writer, string propertyName, T value)
        {
            writer.WritePropertyName(propertyName);
            JsonSerializer.Serialize(writer, value, SerializerOptions);
        }

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.MakeReadOnly(populateMissingResolver: true);
            return options;
        }
    }
}
