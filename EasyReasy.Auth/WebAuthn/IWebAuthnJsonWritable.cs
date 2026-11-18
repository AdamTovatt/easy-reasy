using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// A WebAuthn type that writes its own JSON object, so <see cref="WebAuthnJson"/> can produce its fixed wire
    /// format for both its <c>ToJson</c> and its System.Text.Json converter.
    /// </summary>
    /// <remarks>
    /// The type carries a <c>[JsonConverter]</c>, so handing it to <see cref="JsonSerializer"/> would come straight
    /// back to that converter. Its top level is therefore written here, by hand; the values nested inside it are
    /// written through <see cref="WebAuthnJson.WriteProperty{T}"/>.
    /// </remarks>
    internal interface IWebAuthnJsonWritable
    {
        /// <summary>
        /// Writes this value as one JSON object.
        /// </summary>
        /// <param name="writer">The writer <see cref="WebAuthnJson"/> created with the fixed WebAuthn settings.</param>
        void WriteJson(Utf8JsonWriter writer);
    }
}
