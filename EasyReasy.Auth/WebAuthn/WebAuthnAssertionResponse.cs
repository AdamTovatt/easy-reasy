using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The <c>response</c> member of the credential an authentication ceremony produces — the browser's
    /// <c>AuthenticatorAssertionResponseJSON</c> (WebAuthn Level 3 §5.2.2).
    /// </summary>
    /// <remarks>
    /// Unlike registration, the authenticator data here arrives as a field of its own rather than wrapped
    /// in an attestation object, and it is signed: the signature covers those bytes followed by the hash of
    /// the client data JSON. Both stay in the base64url spelling the browser wrote, because the bytes that
    /// were signed are the bytes that were sent, and a round trip through any other encoding would no
    /// longer hash to what the authenticator signed.
    /// </remarks>
    public sealed class WebAuthnAssertionResponse
    {
        /// <summary>The wire (JSON) name of <see cref="ClientDataJson"/>.</summary>
        public const string ClientDataJsonFieldName = "clientDataJSON";

        /// <summary>The wire (JSON) name of <see cref="AuthenticatorData"/>.</summary>
        public const string AuthenticatorDataFieldName = "authenticatorData";

        /// <summary>The wire (JSON) name of <see cref="Signature"/>.</summary>
        public const string SignatureFieldName = "signature";

        /// <summary>The wire (JSON) name of <see cref="UserHandle"/>.</summary>
        public const string UserHandleFieldName = "userHandle";

        /// <summary>
        /// The client data the browser collected, base64url-encoded. The ceremony's challenge and origin
        /// are read from the JSON inside it, and its hash is half of what was signed.
        /// </summary>
        [JsonPropertyName(ClientDataJsonFieldName)]
        public string ClientDataJson { get; }

        /// <summary>
        /// The authenticator data, base64url-encoded — the relying-party id hash, the flags and the sign
        /// counter. The other half of what was signed.
        /// </summary>
        [JsonPropertyName(AuthenticatorDataFieldName)]
        public string AuthenticatorData { get; }

        /// <summary>
        /// The signature over the authenticator data followed by the SHA-256 of the client data JSON,
        /// base64url-encoded, in the encoding the credential's algorithm uses on the wire.
        /// </summary>
        [JsonPropertyName(SignatureFieldName)]
        public string Signature { get; }

        /// <summary>
        /// The user handle the authenticator stored at registration, base64url-encoded, or null when the
        /// authenticator reported none.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Verified by <see cref="WebAuthnVerifier.VerifyDiscoverableAuthentication"/>, which requires it
        /// and compares it against the credential owner's handle: in usernameless passkey
        /// sign-in no user is known yet, so the handle is part of what says whose credential answered.
        /// </para>
        /// <para>
        /// Not read by the second-factor <see cref="WebAuthnVerifier.VerifyAuthentication"/>. Matching a
        /// user handle answers "which user is this?", which is the question the first factor already
        /// answered; there the application has a session and looked this credential up against it. An
        /// authenticator answering a non-discoverable ceremony is not required to send one at all, so
        /// requiring it would reject the ordinary case.
        /// </para>
        /// </remarks>
        [JsonPropertyName(UserHandleFieldName)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UserHandle { get; }

        /// <summary>The decoded client data JSON bytes, whose hash is signed over.</summary>
        internal byte[] ClientDataJsonBytes { get; }

        /// <summary>The decoded authenticator data bytes, which are signed over directly.</summary>
        internal byte[] AuthenticatorDataBytes { get; }

        /// <summary>The decoded signature.</summary>
        internal byte[] SignatureBytes { get; }

        /// <summary>
        /// The decoded user handle, or null when the authenticator reported none. Decoded leniently, like
        /// every other field the browser sends, so a padded spelling of the right bytes still matches.
        /// </summary>
        internal byte[]? UserHandleBytes { get; }

        /// <summary>
        /// Initializes the assertion response.
        /// </summary>
        /// <param name="clientDataJson">The base64url-encoded collected client data.</param>
        /// <param name="authenticatorData">The base64url-encoded authenticator data.</param>
        /// <param name="signature">The base64url-encoded signature.</param>
        /// <param name="userHandle">The base64url-encoded user handle, or null.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="ArgumentException">An encoded field is empty, too long, or not base64url.</exception>
        public WebAuthnAssertionResponse(string clientDataJson, string authenticatorData, string signature, string? userHandle = null)
        {
            ArgumentNullException.ThrowIfNull(clientDataJson);
            ArgumentNullException.ThrowIfNull(authenticatorData);
            ArgumentNullException.ThrowIfNull(signature);

            ClientDataJsonBytes = WebAuthnResponseReader.Decode(clientDataJson, nameof(clientDataJson));
            AuthenticatorDataBytes = WebAuthnResponseReader.Decode(authenticatorData, nameof(authenticatorData));
            SignatureBytes = WebAuthnResponseReader.Decode(signature, nameof(signature));

            UserHandleBytes = WebAuthnResponseReader.DecodeOptional(userHandle, nameof(userHandle));

            ClientDataJson = clientDataJson;
            AuthenticatorData = authenticatorData;
            Signature = signature;
            UserHandle = userHandle;
        }
    }
}
