using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The client data the browser collected and the authenticator signed over (WebAuthn Level 3 §5.8.1).
    /// Internal: it is a parsing step on the way to the checks the verifier makes.
    /// </summary>
    /// <remarks>
    /// Parsed from the exact bytes the browser sent and never re-serialized. The signature covers the hash
    /// of those bytes, so a round trip through this type could only ever produce a different encoding of
    /// the same fields — which would no longer hash to what was signed.
    /// </remarks>
    internal sealed class CollectedClientData
    {
        /// <summary>The ceremony type a registration produces.</summary>
        public const string RegistrationCeremonyType = "webauthn.create";

        /// <summary>The ceremony type an authentication produces.</summary>
        public const string AuthenticationCeremonyType = "webauthn.get";

        /// <summary>The ceremony type the browser recorded.</summary>
        public string Type { get; }

        /// <summary>The challenge the browser was given, still base64url-encoded as the browser wrote it.</summary>
        public string Challenge { get; }

        /// <summary>The origin of the page that ran the ceremony, in its serialized form.</summary>
        public string Origin { get; }

        /// <summary>
        /// Whether the ceremony ran in a frame whose ancestors are not all same-origin with it — that is,
        /// whether some other site had the relying party's page embedded when the user was prompted.
        /// Absent in the client data means <c>false</c>.
        /// </summary>
        /// <remarks>
        /// The companion <c>topOrigin</c> field is deliberately not read: it only carries meaning under a
        /// policy that names which embedding sites are acceptable, and a second factor has no such policy —
        /// it declines the cross-origin ceremony outright rather than deciding whose frame it was in.
        /// </remarks>
        public bool CrossOrigin { get; }

        private CollectedClientData(string type, string challenge, string origin, bool crossOrigin)
        {
            Type = type;
            Challenge = challenge;
            Origin = origin;
            CrossOrigin = crossOrigin;
        }

        /// <summary>
        /// Parses collected client data from the raw JSON bytes the browser sent.
        /// </summary>
        /// <param name="json">The UTF-8 JSON bytes, exactly as received.</param>
        /// <returns>The parsed client data.</returns>
        /// <exception cref="WebAuthnParseException">The bytes are not a JSON object carrying the three required fields.</exception>
        public static CollectedClientData Parse(ReadOnlySpan<byte> json)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json.ToArray());

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new WebAuthnParseException(WebAuthnParseError.MalformedClientData, "Collected client data is not a JSON object.");
                }

                string type = ReadRequiredString(document.RootElement, "type");
                string challenge = ReadRequiredString(document.RootElement, "challenge");
                string origin = ReadRequiredString(document.RootElement, "origin");
                bool crossOrigin = ReadOptionalBoolean(document.RootElement, "crossOrigin");

                return new CollectedClientData(type, challenge, origin, crossOrigin);
            }
            catch (JsonException exception)
            {
                throw new WebAuthnParseException(WebAuthnParseError.MalformedClientData, $"Collected client data is not valid JSON: {exception.Message}");
            }
        }

        private static string ReadRequiredString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
            {
                throw new WebAuthnParseException(WebAuthnParseError.MalformedClientData, $"Collected client data has no string \"{propertyName}\".");
            }

            string value = property.GetString()!;
            if (value.Length == 0)
            {
                // None of the three can be empty and still mean anything, and rejecting here keeps the
                // verifier's checks from being the place an empty value is first noticed.
                throw new WebAuthnParseException(WebAuthnParseError.MalformedClientData, $"Collected client data has an empty \"{propertyName}\".");
            }

            return value;
        }

        private static bool ReadOptionalBoolean(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
            {
                return false;
            }

            if (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False)
            {
                throw new WebAuthnParseException(WebAuthnParseError.MalformedClientData, $"Collected client data's \"{propertyName}\" is not a boolean.");
            }

            return property.ValueKind == JsonValueKind.True;
        }
    }
}
