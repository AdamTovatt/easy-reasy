using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The converter of <see cref="WebAuthnAuthenticationResponse"/>, which reads through the same parser as
    /// <see cref="WebAuthnAuthenticationResponse.FromJson"/>.
    /// </summary>
    internal sealed class WebAuthnAuthenticationResponseJsonConverter : WebAuthnJsonConverter<WebAuthnAuthenticationResponse>
    {
        /// <inheritdoc />
        /// <exception cref="JsonException">The value is not an authentication response; the message is the parser's.</exception>
        public override WebAuthnAuthenticationResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return WebAuthnAuthenticationResponse.Read(ref reader);
        }
    }
}
