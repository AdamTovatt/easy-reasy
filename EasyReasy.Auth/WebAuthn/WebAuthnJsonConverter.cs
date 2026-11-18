using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// What every WebAuthn converter writes: exactly the value's <c>ToJson</c> bytes, whatever the host's options.
    /// </summary>
    /// <typeparam name="T">The WebAuthn type the converter is attached to.</typeparam>
    internal abstract class WebAuthnJsonConverter<T> : JsonConverter<T>
        where T : class, IWebAuthnJsonWritable
    {
        /// <inheritdoc />
        public sealed override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            // The host's options are ignored on purpose (see WebAuthnJson). Written raw, so the bytes stay exactly
            // ToJson's even inside a host document that is indented or uses another encoder: there the value is a
            // compact island, which is still valid JSON.
            writer.WriteRawValue(WebAuthnJson.ToUtf8Bytes(value), skipInputValidation: true);
        }
    }
}
