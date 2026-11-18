using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The converter of <see cref="WebAuthnRegistrationResponse"/>, which reads through the same parser as
    /// <see cref="WebAuthnRegistrationResponse.FromJson"/>.
    /// </summary>
    /// <remarks>
    /// One small converter per credential type rather than one generic: a generic one would need the parse as a
    /// static abstract member of an internal interface, which costs more than these few lines save.
    /// </remarks>
    internal sealed class WebAuthnRegistrationResponseJsonConverter : WebAuthnJsonConverter<WebAuthnRegistrationResponse>
    {
        /// <inheritdoc />
        /// <exception cref="JsonException">The value is not a registration response; the message is the parser's.</exception>
        public override WebAuthnRegistrationResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return WebAuthnRegistrationResponse.Read(ref reader);
        }
    }
}
