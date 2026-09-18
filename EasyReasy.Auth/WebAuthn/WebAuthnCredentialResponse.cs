using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// What both WebAuthn ceremonies post back, minus the part that differs: the credential's identity, its
    /// type, and how the authenticator was attached, as the browser's <c>PublicKeyCredential.toJSON()</c>
    /// writes them (WebAuthn Level 3 §5.1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two ceremonies return the same <c>PublicKeyCredential</c> in the specification and differ only in
    /// what its <c>response</c> member holds — an attestation for a registration, an assertion for an
    /// authentication. This type is that shared envelope, so the guards over it are written and fixed once:
    /// two copies of a validating constructor is one place to harden and one to forget, and here the values
    /// being validated are attacker-supplied.
    /// </para>
    /// <para>
    /// Constructed from the raw JSON through the derived types' <c>FromJson</c>, not from already-decoded
    /// <c>byte[]</c> arguments. Every field the browser sends is base64url, and base64url-versus-base64 is
    /// where WebAuthn integrations go wrong; taking decoded bytes would hand that decision — and the bug —
    /// to every application in turn.
    /// </para>
    /// </remarks>
    public abstract class WebAuthnCredentialResponse
    {
        /// <summary>The wire (JSON) name of <see cref="Id"/>.</summary>
        public const string IdFieldName = "id";

        /// <summary>The wire (JSON) name of <see cref="RawId"/>.</summary>
        public const string RawIdFieldName = "rawId";

        /// <summary>The wire (JSON) name of <see cref="Type"/>.</summary>
        public const string TypeFieldName = "type";

        /// <summary>The wire (JSON) name of the derived types' response member.</summary>
        public const string ResponseFieldName = "response";

        /// <summary>The wire (JSON) name of <see cref="AuthenticatorAttachment"/>.</summary>
        public const string AuthenticatorAttachmentFieldName = "authenticatorAttachment";

        /// <summary>
        /// Longest credential type this library will quote back in an error message.
        /// </summary>
        /// <remarks>
        /// The type is the one field of the envelope whose value is repeated verbatim into an
        /// <see cref="ArgumentException"/>, and it arrives from the network. Bounded for the same reason
        /// <see cref="CollectedClientData"/> bounds the fields it quotes: an application is told to log
        /// these messages, so a value long enough to bury a log line, or carrying newlines to forge one,
        /// must not reach the message intact.
        /// </remarks>
        public const int MaximumQuotedTypeLength = 64;

        /// <summary>The credential id, base64url-encoded, exactly as the browser wrote it.</summary>
        [JsonPropertyName(IdFieldName)]
        public string Id { get; }

        /// <summary>
        /// The same credential id, base64url-encoded — what <c>rawId</c> is in the browser's JSON. The two
        /// are the same bytes by construction, and a response whose <c>id</c> and <c>rawId</c> disagree is
        /// rejected at construction rather than leaving a choice of which one the credential is.
        /// </summary>
        [JsonPropertyName(RawIdFieldName)]
        public string RawId { get; }

        /// <summary>The credential type, which WebAuthn defines exactly one of: <c>public-key</c>.</summary>
        [JsonPropertyName(TypeFieldName)]
        public string Type { get; }

        /// <summary>
        /// How the authenticator was attached — <c>platform</c> for Touch ID, Face ID or Windows Hello,
        /// <c>cross-platform</c> for a security key — or null when the browser reported nothing.
        /// </summary>
        /// <remarks>
        /// A string rather than <see cref="WebAuthnAuthenticatorAttachment"/> on purpose: it is
        /// informational, the browser is free to report a value this library does not know, and turning an
        /// unrecognised one into a rejected ceremony would fail it over a label nothing checks.
        /// </remarks>
        [JsonPropertyName(AuthenticatorAttachmentFieldName)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AuthenticatorAttachment { get; }

        /// <summary>The decoded credential id bytes, for comparison against the authenticator's own copy.</summary>
        internal byte[] CredentialIdBytes { get; }

        /// <summary>
        /// Validates and stores the envelope both ceremonies share.
        /// </summary>
        /// <param name="id">The base64url credential id.</param>
        /// <param name="rawId">The base64url credential id again, which must stand for the same bytes as <paramref name="id"/>.</param>
        /// <param name="type">The credential type, which must be <c>public-key</c>.</param>
        /// <param name="authenticatorAttachment">How the authenticator was attached, or null.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="ArgumentException">
        /// An id is empty, too long or not base64url, the two ids stand for different bytes, or the type is
        /// not <c>public-key</c>.
        /// </exception>
        protected WebAuthnCredentialResponse(string id, string rawId, string type, string? authenticatorAttachment)
        {
            ArgumentNullException.ThrowIfNull(id);
            ArgumentNullException.ThrowIfNull(rawId);
            ArgumentNullException.ThrowIfNull(type);

            CredentialIdBytes = WebAuthnResponseReader.Decode(id, nameof(id));
            byte[] rawIdBytes = WebAuthnResponseReader.Decode(rawId, nameof(rawId));

            if (!CredentialIdBytes.AsSpan().SequenceEqual(rawIdBytes))
            {
                throw new ArgumentException(
                    $"'{nameof(id)}' and '{nameof(rawId)}' are the same credential id in the browser's JSON, but these stand for different bytes.",
                    nameof(rawId));
            }

            if (!string.Equals(type, PublicKeyCredentialType.PublicKey, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"'{nameof(type)}' must be \"{PublicKeyCredentialType.PublicKey}\", the only credential type WebAuthn defines; got {DescribeType(type)}.",
                    nameof(type));
            }

            Id = id;
            RawId = rawId;
            Type = type;
            AuthenticatorAttachment = authenticatorAttachment;
        }

        /// <summary>
        /// Serializes this response to JSON, in the shape the browser produced it.
        /// </summary>
        /// <returns>The JSON representation.</returns>
        /// <remarks>
        /// Implemented by each derived type rather than here, because <c>JsonSerializer.Serialize</c>
        /// writes the properties of the type it is handed: called on this one it would serialize the
        /// envelope and silently drop the response member that is the point of the document.
        /// </remarks>
        public abstract string ToJson();

        /// <inheritdoc />
        public override string ToString()
        {
            return ToJson();
        }

        /// <summary>
        /// Renders a rejected credential type for an error message, quoting it only while it is short
        /// enough and free of the characters that would let it forge a log line of its own.
        /// </summary>
        private static string DescribeType(string type)
        {
            if (type.Length > MaximumQuotedTypeLength)
            {
                return $"a value of {type.Length} characters";
            }

            foreach (char character in type)
            {
                if (char.IsControl(character))
                {
                    return $"a value of {type.Length} characters carrying a control character";
                }
            }

            return $"\"{type}\"";
        }
    }
}
