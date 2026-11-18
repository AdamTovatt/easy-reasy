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
    /// The envelope — the credential id, the type, the attachment, and the guards over them — is
    /// <see cref="WebAuthnCredentialResponse"/>'s. What a registration adds is the attestation the
    /// authenticator produced.
    /// <para>
    /// It binds directly as a property of a request model: its System.Text.Json converter reads through the same
    /// parser as <see cref="FromJson"/>, and answers a credential that is present but malformed with a
    /// <see cref="JsonException"/>, which ASP.NET model binding answers with 400. A <c>null</c> or missing member never
    /// reaches the converter and binds as <c>null</c>, so check for it. Writing it produces exactly <see cref="WebAuthnCredentialResponse.ToJson"/>.
    /// </para>
    /// </remarks>
    [JsonConverter(typeof(WebAuthnRegistrationResponseJsonConverter))]
    public sealed class WebAuthnRegistrationResponse : WebAuthnCredentialResponse
    {
        private const string DocumentName = "A registration response";

        /// <summary>The attestation response carrying the client data and the attestation object.</summary>
        public WebAuthnAttestationResponse Response { get; }

        /// <summary>
        /// Initializes the registration response.
        /// </summary>
        /// <param name="id">The base64url credential id.</param>
        /// <param name="rawId">The base64url credential id again, which must stand for the same bytes as <paramref name="id"/>.</param>
        /// <param name="type">The credential type, which must be <c>public-key</c>.</param>
        /// <param name="response">The attestation response.</param>
        /// <param name="authenticatorAttachment">How the authenticator was attached, or null.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="ArgumentException">
        /// An id is empty, too long or not base64url, the two ids stand for different bytes, or the type is
        /// not <c>public-key</c>.
        /// </exception>
        public WebAuthnRegistrationResponse(
            string id,
            string rawId,
            string type,
            WebAuthnAttestationResponse response,
            string? authenticatorAttachment = null)
            : base(id, rawId, type, authenticatorAttachment)
        {
            ArgumentNullException.ThrowIfNull(response);

            Response = response;
        }

        private protected override void WriteResponse(Utf8JsonWriter writer)
        {
            WebAuthnJson.WriteProperty(writer, ResponseFieldName, Response);
        }

        /// <summary>
        /// Parses the body a page posts back after <c>navigator.credentials.create()</c>.
        /// </summary>
        /// <param name="json">The JSON the browser's <c>toJSON()</c> produced.</param>
        /// <returns>The parsed response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The text is not a registration response. A ceremony that fails a <i>verification</i> check is a
        /// <see cref="WebAuthnRegistrationResult"/> instead, never an exception.
        /// </exception>
        public static WebAuthnRegistrationResponse FromJson(string json)
        {
            ArgumentNullException.ThrowIfNull(json);

            using JsonDocument document = WebAuthnResponseReader.ParseObject(json, DocumentName);
            return ReadObject(document.RootElement);
        }

        /// <summary>
        /// Reads a registration response nested in a larger document, for its System.Text.Json converter, by the
        /// same rules as <see cref="FromJson"/>.
        /// </summary>
        /// <param name="reader">The reader, on the first token of the value.</param>
        /// <returns>The parsed response.</returns>
        /// <exception cref="JsonException">The value is not a registration response; the message is the parser's.</exception>
        internal static WebAuthnRegistrationResponse Read(ref Utf8JsonReader reader)
        {
            return WebAuthnResponseReader.ReadNested(ref reader, DocumentName, ReadObject);
        }

        private static WebAuthnRegistrationResponse ReadObject(JsonElement root)
        {
            // Read field by field rather than through the serializer's own binding: the browser writes this
            // JSON, so a missing field has to name itself in the message an endpoint logs, and the
            // constructor's invariants have to run rather than be bypassed by property initialization.
            JsonElement responseElement = WebAuthnResponseReader.ReadRequiredObject(root, ResponseFieldName, DocumentName);
            string responseSubject = $"{DocumentName}'s \"{ResponseFieldName}\"";

            WebAuthnAttestationResponse response = new WebAuthnAttestationResponse(
                WebAuthnResponseReader.ReadRequiredString(responseElement, WebAuthnAttestationResponse.ClientDataJsonFieldName, responseSubject),
                WebAuthnResponseReader.ReadRequiredString(responseElement, WebAuthnAttestationResponse.AttestationObjectFieldName, responseSubject),
                WebAuthnResponseReader.ReadOptionalStringArray(responseElement, WebAuthnAttestationResponse.TransportsFieldName, responseSubject));

            return new WebAuthnRegistrationResponse(
                WebAuthnResponseReader.ReadRequiredString(root, IdFieldName, DocumentName),
                WebAuthnResponseReader.ReadRequiredString(root, RawIdFieldName, DocumentName),
                WebAuthnResponseReader.ReadRequiredString(root, TypeFieldName, DocumentName),
                response,
                WebAuthnResponseReader.ReadOptionalString(root, AuthenticatorAttachmentFieldName, DocumentName));
        }
    }
}
