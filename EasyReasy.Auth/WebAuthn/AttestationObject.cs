using System.Formats.Cbor;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The attestation object a registration ceremony returns (WebAuthn Level 3 §6.5): a CBOR map of the
    /// attestation statement format, the statement itself and the authenticator data. Internal: only the
    /// authenticator data leaves the parser.
    /// </summary>
    /// <remarks>
    /// This library verifies the <c>none</c> format only, and requires the statement to be the empty map
    /// that format defines (WebAuthn Level 3 §8.7). A statement carrying anything is a response whose format
    /// and content disagree, which no honest authenticator produces — so it is rejected rather than ignored.
    /// </remarks>
    internal sealed class AttestationObject
    {
        /// <summary>The only attestation statement format this library accepts.</summary>
        public const string NoneFormat = "none";

        private const string FormatKey = "fmt";
        private const string AttestationStatementKey = "attStmt";
        private const string AuthenticatorDataKey = "authData";

        /// <summary>The parsed authenticator data the attestation object carried.</summary>
        public AuthenticatorData AuthenticatorData { get; }

        private AttestationObject(AuthenticatorData authenticatorData)
        {
            AuthenticatorData = authenticatorData;
        }

        /// <summary>
        /// Parses an attestation object and the authenticator data inside it.
        /// </summary>
        /// <param name="encoded">The CBOR-encoded attestation object.</param>
        /// <returns>The parsed attestation object.</returns>
        /// <exception cref="WebAuthnParseException">
        /// The bytes are not a well-formed attestation object, or its format is not <c>none</c>.
        /// </exception>
        public static AttestationObject Parse(ReadOnlyMemory<byte> encoded)
        {
            string? format = null;
            byte[]? authenticatorDataBytes = null;
            bool attestationStatementIsEmpty = false;
            bool sawAttestationStatement = false;
            HashSet<string> seenKeys = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                CborReader reader = new CborReader(encoded, CborConformanceMode.Strict);
                reader.ReadStartMap();

                while (reader.PeekState() != CborReaderState.EndMap)
                {
                    if (reader.PeekState() != CborReaderState.TextString)
                    {
                        // The keys this library reads are all text strings; anything else is an extension
                        // it does not understand, and skipping is what keeps a future CTAP addition from
                        // breaking a ceremony.
                        reader.SkipValue();
                        reader.SkipValue();
                        continue;
                    }

                    string key = reader.ReadTextString();

                    // Strict mode rejects a repeated key only when the two encodings are byte-identical, so
                    // the same key written with a minimal and a non-minimal length header reaches here
                    // twice. Without this, a "fmt" of "packed" followed by one of "none" would parse as
                    // "none" and the format check would pass on a statement it never inspected.
                    if (!seenKeys.Add(key))
                    {
                        throw new WebAuthnParseException(WebAuthnParseError.MalformedAttestationObject, $"Attestation object repeats the key \"{key}\".");
                    }

                    switch (key)
                    {
                        case FormatKey:
                            format = reader.ReadTextString();
                            break;
                        case AuthenticatorDataKey:
                            authenticatorDataBytes = reader.ReadByteString();
                            break;
                        case AttestationStatementKey:
                            sawAttestationStatement = true;
                            attestationStatementIsEmpty = ReadIsEmptyMap(reader);
                            break;
                        default:
                            reader.SkipValue();
                            break;
                    }
                }

                reader.ReadEndMap();

                if (reader.BytesRemaining != 0)
                {
                    throw new WebAuthnParseException(
                        WebAuthnParseError.MalformedAttestationObject,
                        $"Attestation object is followed by {reader.BytesRemaining} bytes that are not part of it.");
                }
            }
            catch (Exception exception) when (exception is CborContentException or InvalidOperationException)
            {
                throw WebAuthnParseException.FromCborFailure(WebAuthnParseError.MalformedAttestationObject, "Attestation object", exception);
            }

            if (format == null)
            {
                throw new WebAuthnParseException(WebAuthnParseError.MalformedAttestationObject, $"Attestation object has no \"{FormatKey}\".");
            }

            if (!string.Equals(format, NoneFormat, StringComparison.Ordinal))
            {
                throw new WebAuthnParseException(
                    WebAuthnParseError.UnsupportedAttestationFormat,
                    $"Attestation statement format \"{format}\" is not verified by this library; register with attestation \"{NoneFormat}\".");
            }

            if (!sawAttestationStatement || !attestationStatementIsEmpty)
            {
                throw new WebAuthnParseException(
                    WebAuthnParseError.MalformedAttestationObject,
                    $"Attestation object states format \"{NoneFormat}\", whose statement is defined as an empty map, but carries something else.");
            }

            if (authenticatorDataBytes == null)
            {
                throw new WebAuthnParseException(WebAuthnParseError.MalformedAttestationObject, $"Attestation object has no \"{AuthenticatorDataKey}\".");
            }

            return new AttestationObject(AuthenticatorData.Parse(authenticatorDataBytes));
        }

        /// <summary>
        /// Reads the next value and reports whether it was an empty CBOR map.
        /// </summary>
        private static bool ReadIsEmptyMap(CborReader reader)
        {
            if (reader.PeekState() != CborReaderState.StartMap)
            {
                reader.SkipValue();
                return false;
            }

            reader.ReadStartMap();
            bool isEmpty = reader.PeekState() == CborReaderState.EndMap;
            while (reader.PeekState() != CborReaderState.EndMap)
            {
                reader.SkipValue();
                reader.SkipValue();
            }

            reader.ReadEndMap();
            return isEmpty;
        }
    }
}
