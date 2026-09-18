using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The credential a registration ceremony produced, as the browser's
    /// <c>PublicKeyCredential.toJSON()</c> writes it (WebAuthn Level 3 §5.1). This is the body a page posts
    /// back after <c>navigator.credentials.create()</c> resolves.
    /// </summary>
    /// <remarks>
    /// Constructed from the raw JSON through <see cref="FromJson"/>, not from already-decoded
    /// <c>byte[]</c> arguments. Every field the browser sends here is base64url, and base64url-versus-base64
    /// is where WebAuthn integrations go wrong; taking decoded bytes would hand that decision — and the bug
    /// — to every application in turn.
    /// </remarks>
    public sealed class WebAuthnRegistrationResponse
    {
        /// <summary>The wire (JSON) name of <see cref="Id"/>.</summary>
        public const string IdFieldName = "id";

        /// <summary>The wire (JSON) name of <see cref="RawId"/>.</summary>
        public const string RawIdFieldName = "rawId";

        /// <summary>The wire (JSON) name of <see cref="Type"/>.</summary>
        public const string TypeFieldName = "type";

        /// <summary>The wire (JSON) name of <see cref="Response"/>.</summary>
        public const string ResponseFieldName = "response";

        /// <summary>The wire (JSON) name of <see cref="AuthenticatorAttachment"/>.</summary>
        public const string AuthenticatorAttachmentFieldName = "authenticatorAttachment";

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

        /// <summary>The attestation response carrying the client data and the attestation object.</summary>
        [JsonPropertyName(ResponseFieldName)]
        public WebAuthnAttestationResponse Response { get; }

        /// <summary>
        /// How the authenticator was attached — <c>platform</c> for Touch ID, Face ID or Windows Hello,
        /// <c>cross-platform</c> for a security key — or null when the browser reported nothing.
        /// </summary>
        /// <remarks>
        /// A string rather than <see cref="WebAuthnAuthenticatorAttachment"/> on purpose: it is
        /// informational, the browser is free to report a value this library does not know, and turning an
        /// unrecognised one into a rejected registration would fail a ceremony over a label nothing checks.
        /// </remarks>
        [JsonPropertyName(AuthenticatorAttachmentFieldName)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AuthenticatorAttachment { get; }

        /// <summary>The decoded credential id bytes, for comparison against the authenticator's own copy.</summary>
        internal byte[] CredentialIdBytes { get; }

        /// <summary>
        /// Initializes the registration response.
        /// </summary>
        /// <param name="id">The base64url credential id.</param>
        /// <param name="rawId">The base64url credential id again, which must stand for the same bytes as <paramref name="id"/>.</param>
        /// <param name="type">The credential type, which must be <c>public-key</c>.</param>
        /// <param name="response">The attestation response.</param>
        /// <param name="authenticatorAttachment">How the authenticator was attached, or null.</param>
        /// <exception cref="ArgumentNullException">Any required argument is null.</exception>
        /// <exception cref="ArgumentException">
        /// An id is empty or not base64url, the two ids stand for different bytes, or the type is not
        /// <c>public-key</c>.
        /// </exception>
        public WebAuthnRegistrationResponse(
            string id,
            string rawId,
            string type,
            WebAuthnAttestationResponse response,
            string? authenticatorAttachment = null)
        {
            ArgumentNullException.ThrowIfNull(id);
            ArgumentNullException.ThrowIfNull(rawId);
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(response);

            CredentialIdBytes = WebAuthnResponseField.Decode(id, nameof(id));
            byte[] rawIdBytes = WebAuthnResponseField.Decode(rawId, nameof(rawId));

            if (!CredentialIdBytes.AsSpan().SequenceEqual(rawIdBytes))
            {
                throw new ArgumentException(
                    $"'{nameof(id)}' and '{nameof(rawId)}' are the same credential id in the browser's JSON, but these stand for different bytes.",
                    nameof(rawId));
            }

            if (!string.Equals(type, PublicKeyCredentialType.PublicKey, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"'{nameof(type)}' must be \"{PublicKeyCredentialType.PublicKey}\", the only credential type WebAuthn defines; got \"{type}\".",
                    nameof(type));
            }

            Id = id;
            RawId = rawId;
            Type = type;
            Response = response;
            AuthenticatorAttachment = authenticatorAttachment;
        }

        /// <summary>
        /// Serializes this response to JSON, in the shape the browser produced it.
        /// </summary>
        /// <returns>The JSON representation.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonSerializerSettings.CurrentOptions);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return ToJson();
        }

        /// <summary>
        /// Parses the body a page posts back after <c>navigator.credentials.create()</c>.
        /// </summary>
        /// <param name="json">The JSON the browser's <c>toJSON()</c> produced.</param>
        /// <returns>The parsed response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The text is not a registration response: not JSON, missing a required field, or carrying one
        /// that is not what WebAuthn defines it to be. A ceremony that fails a <i>verification</i> check is
        /// a <see cref="WebAuthnRegistrationResult"/> instead, never an exception.
        /// </exception>
        public static WebAuthnRegistrationResponse FromJson(string json)
        {
            ArgumentNullException.ThrowIfNull(json);

            const string what = "A registration response";

            // Read field by field rather than through the serializer's own binding: the browser writes this
            // JSON, so a missing field has to name itself in the message an endpoint logs, and the
            // constructor's invariants have to run rather than be bypassed by property initialization.
            using JsonDocument document = WebAuthnResponseField.ParseObject(json, what);
            JsonElement root = document.RootElement;

            JsonElement responseElement = WebAuthnResponseField.ReadRequiredObject(root, ResponseFieldName, what);

            WebAuthnAttestationResponse response = new WebAuthnAttestationResponse(
                WebAuthnResponseField.ReadRequiredString(responseElement, WebAuthnAttestationResponse.ClientDataJsonFieldName, $"{what}'s \"{ResponseFieldName}\""),
                WebAuthnResponseField.ReadRequiredString(responseElement, WebAuthnAttestationResponse.AttestationObjectFieldName, $"{what}'s \"{ResponseFieldName}\""),
                WebAuthnResponseField.ReadOptionalStringArray(responseElement, WebAuthnAttestationResponse.TransportsFieldName, $"{what}'s \"{ResponseFieldName}\""));

            return new WebAuthnRegistrationResponse(
                WebAuthnResponseField.ReadRequiredString(root, IdFieldName, what),
                WebAuthnResponseField.ReadRequiredString(root, RawIdFieldName, what),
                WebAuthnResponseField.ReadRequiredString(root, TypeFieldName, what),
                response,
                WebAuthnResponseField.ReadOptionalString(root, AuthenticatorAttachmentFieldName, what));
        }
    }
}
