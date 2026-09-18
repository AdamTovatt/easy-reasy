using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The <c>response</c> member of the credential a registration ceremony produces — the browser's
    /// <c>AuthenticatorAttestationResponseJSON</c> (WebAuthn Level 3 §5.2.1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two fields this type carries are the whole of what registration verifies. The browser also emits
    /// <c>authenticatorData</c>, <c>publicKey</c> and <c>publicKeyAlgorithm</c> alongside them as a
    /// convenience, and they are deliberately not modelled: each is a second copy of something already
    /// inside the attestation object, so accepting them would create a pair of values that can disagree and
    /// a verifier that might check the wrong one. What is stored is read out of the attestation object,
    /// which is the copy the authenticator itself assembled.
    /// </para>
    /// <para>
    /// Both fields stay in the base64url spelling the browser wrote, so the object serializes back to what
    /// was received. Decoding belongs to verification.
    /// </para>
    /// </remarks>
    public sealed class WebAuthnAttestationResponse
    {
        /// <summary>
        /// Most transports this library will accept from one authenticator. The specification names six.
        /// </summary>
        public const int MaximumTransportCount = 16;

        /// <summary>
        /// Longest transport name this library will accept. The longest the specification defines,
        /// <c>smart-card</c>, is ten characters.
        /// </summary>
        public const int MaximumTransportLength = 32;

        /// <summary>The wire (JSON) name of <see cref="ClientDataJson"/>.</summary>
        public const string ClientDataJsonFieldName = "clientDataJSON";

        /// <summary>The wire (JSON) name of <see cref="AttestationObject"/>.</summary>
        public const string AttestationObjectFieldName = "attestationObject";

        /// <summary>The wire (JSON) name of <see cref="Transports"/>.</summary>
        public const string TransportsFieldName = "transports";

        /// <summary>
        /// The client data the browser collected, base64url-encoded. The JSON inside it is what the
        /// ceremony's challenge and origin are read from.
        /// </summary>
        [JsonPropertyName(ClientDataJsonFieldName)]
        public string ClientDataJson { get; }

        /// <summary>
        /// The CBOR attestation object the authenticator produced, base64url-encoded. The credential id,
        /// public key, sign counter and AAGUID are read from the authenticator data inside it.
        /// </summary>
        [JsonPropertyName(AttestationObjectFieldName)]
        public string AttestationObject { get; }

        /// <summary>
        /// How this authenticator can be reached — <c>internal</c>, <c>usb</c>, <c>nfc</c>, <c>ble</c>,
        /// <c>hybrid</c> — as the browser reported it, or null when it reported nothing.
        /// </summary>
        /// <remarks>
        /// Carried through to <see cref="WebAuthnRegisteredCredential.Transports"/> for the application to
        /// store, and read by nothing here. They appear in exactly one place — this response — so an
        /// application that does not keep them cannot recover them later. The set of legal values grows
        /// with the spec, so an unrecognised one is kept rather than rejected — but the count and the
        /// length of each are bounded, because "we do not know the vocabulary" is not a reason to store
        /// whatever arrives. This is the only value in a registered credential that is not fixed-width or
        /// bounded by its own structure, and the one an application is told to persist.
        /// </remarks>
        [JsonPropertyName(TransportsFieldName)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? Transports { get; }

        /// <summary>
        /// The decoded client data JSON bytes. Decoded once here rather than at each use, because the
        /// signature in an authentication ceremony covers a hash of exactly these bytes.
        /// </summary>
        internal byte[] ClientDataJsonBytes { get; }

        /// <summary>The decoded attestation object bytes.</summary>
        internal byte[] AttestationObjectBytes { get; }

        /// <summary>
        /// Initializes the attestation response.
        /// </summary>
        /// <param name="clientDataJson">The base64url-encoded collected client data.</param>
        /// <param name="attestationObject">The base64url-encoded attestation object.</param>
        /// <param name="transports">The transports the browser reported, or null.</param>
        /// <exception cref="ArgumentNullException"><paramref name="clientDataJson"/> or <paramref name="attestationObject"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Either encoded field is empty or is not base64url, or the transports are too many, too long, or
        /// contain a null.
        /// </exception>
        public WebAuthnAttestationResponse(string clientDataJson, string attestationObject, IEnumerable<string>? transports = null)
        {
            ArgumentNullException.ThrowIfNull(clientDataJson);
            ArgumentNullException.ThrowIfNull(attestationObject);

            ClientDataJsonBytes = WebAuthnResponseReader.Decode(clientDataJson, nameof(clientDataJson));
            AttestationObjectBytes = WebAuthnResponseReader.Decode(attestationObject, nameof(attestationObject));

            ClientDataJson = clientDataJson;
            AttestationObject = attestationObject;
            Transports = CheckTransports(transports, nameof(transports));
        }

        /// <summary>
        /// Bounds the reported transports before they become a value an application stores.
        /// </summary>
        /// <remarks>
        /// Generous against reality — the specification names six, each under a dozen characters — and the
        /// point is only that a bound exists. Without one, a response inside the 64 KB envelope can carry
        /// thousands of transports or a single one tens of thousands of characters long, and the README
        /// tells applications to write this straight to a database column.
        /// </remarks>
        private static IReadOnlyList<string>? CheckTransports(IEnumerable<string>? transports, string parameterName)
        {
            if (transports == null)
            {
                return null;
            }

            string[] reported = transports.ToArray();

            if (reported.Length > MaximumTransportCount)
            {
                throw new ArgumentException(
                    $"'{parameterName}' carries {reported.Length} transports; no authenticator reports more than {MaximumTransportCount}.",
                    parameterName);
            }

            foreach (string transport in reported)
            {
                if (transport == null)
                {
                    throw new ArgumentException($"'{parameterName}' contains a null transport.", parameterName);
                }

                if (transport.Length > MaximumTransportLength)
                {
                    throw new ArgumentException(
                        $"'{parameterName}' contains a transport of {transport.Length} characters; none is longer than {MaximumTransportLength}.",
                        parameterName);
                }
            }

            return reported;
        }
    }
}
