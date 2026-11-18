using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The converter of the WebAuthn options types, which the server writes to the browser and never reads back.
    /// </summary>
    /// <typeparam name="T">The options type.</typeparam>
    internal sealed class WebAuthnOptionsJsonConverter<T> : WebAuthnJsonConverter<T>
        where T : class, IWebAuthnJsonWritable
    {
        /// <inheritdoc />
        /// <exception cref="NotSupportedException">Always: options are only ever written.</exception>
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException(
                $"{typeof(T).Name} is only ever written: the server issues WebAuthn options to the browser and never reads them back. " +
                "Keep the challenge and the user-verification requirement instead, which verification takes as arguments.");
        }
    }
}
