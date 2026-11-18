using System.Buffers.Text;
using System.Text.Json;

namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// Writes the collected client data a browser writes for a ceremony (WebAuthn Level 3 §5.8.1), in the
    /// member order browsers use.
    /// </summary>
    internal static class WebAuthnTestClientData
    {
        /// <summary>
        /// Writes the client data JSON as UTF-8 bytes, the form the authenticator's signature covers.
        /// </summary>
        /// <param name="ceremonyType">The ceremony type, <c>webauthn.create</c> or <c>webauthn.get</c>.</param>
        /// <param name="challenge">The decoded challenge from the options, re-encoded here as the browser does.</param>
        /// <param name="origin">The origin the ceremony runs at, already serialized as a browser writes it.</param>
        public static byte[] Write(string ceremonyType, byte[] challenge, string origin)
        {
            using MemoryStream stream = new MemoryStream();
            using (Utf8JsonWriter writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("type", ceremonyType);
                writer.WriteString("challenge", Base64Url.EncodeToString(challenge));
                writer.WriteString("origin", origin);
                writer.WriteBoolean("crossOrigin", false);
                writer.WriteEndObject();
            }

            return stream.ToArray();
        }
    }
}
