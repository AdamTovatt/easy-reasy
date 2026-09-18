using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The credential an authentication ceremony produced, as the browser's
    /// <c>PublicKeyCredential.toJSON()</c> writes it (WebAuthn Level 3 §5.1). This is the body a page posts
    /// back after <c>navigator.credentials.get()</c> resolves.
    /// </summary>
    /// <remarks>
    /// Constructed from the raw JSON through <see cref="FromJson"/> for the same reason its registration
    /// counterpart is: every field here is base64url, and taking already-decoded bytes would hand that
    /// decision to each application in turn.
    /// </remarks>
    public sealed class WebAuthnAuthenticationResponse
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

        /// <summary>
        /// The credential id the authenticator answered with, base64url-encoded. This is the value to look
        /// the stored credential up by.
        /// </summary>
        [JsonPropertyName(IdFieldName)]
        public string Id { get; }

        /// <summary>
        /// The same credential id, base64url-encoded. A response whose <c>id</c> and <c>rawId</c> stand for
        /// different bytes is rejected at construction rather than leaving a choice of which one it is.
        /// </summary>
        [JsonPropertyName(RawIdFieldName)]
        public string RawId { get; }

        /// <summary>The credential type, which WebAuthn defines exactly one of: <c>public-key</c>.</summary>
        [JsonPropertyName(TypeFieldName)]
        public string Type { get; }

        /// <summary>The assertion carrying the client data, authenticator data and signature.</summary>
        [JsonPropertyName(ResponseFieldName)]
        public WebAuthnAssertionResponse Response { get; }

        /// <summary>
        /// How the authenticator was attached, or null when the browser reported nothing. Informational; a
        /// string rather than a <see cref="WebAuthnAuthenticatorAttachment"/> for the reason given on
        /// <see cref="WebAuthnRegistrationResponse.AuthenticatorAttachment"/>.
        /// </summary>
        [JsonPropertyName(AuthenticatorAttachmentFieldName)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AuthenticatorAttachment { get; }

        /// <summary>The decoded credential id bytes, for comparison against the stored credential's.</summary>
        internal byte[] CredentialIdBytes { get; }

        /// <summary>
        /// Initializes the authentication response.
        /// </summary>
        /// <param name="id">The base64url credential id.</param>
        /// <param name="rawId">The base64url credential id again, which must stand for the same bytes as <paramref name="id"/>.</param>
        /// <param name="type">The credential type, which must be <c>public-key</c>.</param>
        /// <param name="response">The assertion response.</param>
        /// <param name="authenticatorAttachment">How the authenticator was attached, or null.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="ArgumentException">
        /// An id is empty or not base64url, the two ids stand for different bytes, or the type is not
        /// <c>public-key</c>.
        /// </exception>
        public WebAuthnAuthenticationResponse(
            string id,
            string rawId,
            string type,
            WebAuthnAssertionResponse response,
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
        /// Parses the body a page posts back after <c>navigator.credentials.get()</c>.
        /// </summary>
        /// <param name="json">The JSON the browser's <c>toJSON()</c> produced.</param>
        /// <returns>The parsed response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The text is not an authentication response. A ceremony that fails a <i>verification</i> check is
        /// a <see cref="WebAuthnAuthenticationResult"/> instead, never an exception.
        /// </exception>
        public static WebAuthnAuthenticationResponse FromJson(string json)
        {
            ArgumentNullException.ThrowIfNull(json);

            const string what = "An authentication response";

            using JsonDocument document = WebAuthnResponseField.ParseObject(json, what);
            JsonElement root = document.RootElement;

            JsonElement responseElement = WebAuthnResponseField.ReadRequiredObject(root, ResponseFieldName, what);
            string responseSubject = $"{what}'s \"{ResponseFieldName}\"";

            WebAuthnAssertionResponse response = new WebAuthnAssertionResponse(
                WebAuthnResponseField.ReadRequiredString(responseElement, WebAuthnAssertionResponse.ClientDataJsonFieldName, responseSubject),
                WebAuthnResponseField.ReadRequiredString(responseElement, WebAuthnAssertionResponse.AuthenticatorDataFieldName, responseSubject),
                WebAuthnResponseField.ReadRequiredString(responseElement, WebAuthnAssertionResponse.SignatureFieldName, responseSubject),
                WebAuthnResponseField.ReadOptionalString(responseElement, WebAuthnAssertionResponse.UserHandleFieldName, responseSubject));

            return new WebAuthnAuthenticationResponse(
                WebAuthnResponseField.ReadRequiredString(root, IdFieldName, what),
                WebAuthnResponseField.ReadRequiredString(root, RawIdFieldName, what),
                WebAuthnResponseField.ReadRequiredString(root, TypeFieldName, what),
                response,
                WebAuthnResponseField.ReadOptionalString(root, AuthenticatorAttachmentFieldName, what));
        }
    }
}
