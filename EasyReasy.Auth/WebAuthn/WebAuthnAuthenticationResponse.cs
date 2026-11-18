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
    /// The envelope — the credential id, the type, the attachment, and the guards over them — is
    /// <see cref="WebAuthnCredentialResponse"/>'s. What an authentication adds is the assertion the
    /// authenticator signed.
    /// <para>
    /// It binds directly as a property of a request model: its System.Text.Json converter reads through the same
    /// parser as <see cref="FromJson"/>, and answers a credential that is present but malformed with a
    /// <see cref="JsonException"/>, which ASP.NET model binding answers with 400. A <c>null</c> or missing member never
    /// reaches the converter and binds as <c>null</c>, so check for it. Writing it produces exactly <see cref="WebAuthnCredentialResponse.ToJson"/>.
    /// </para>
    /// </remarks>
    [JsonConverter(typeof(WebAuthnAuthenticationResponseJsonConverter))]
    public sealed class WebAuthnAuthenticationResponse : WebAuthnCredentialResponse
    {
        private const string DocumentName = "An authentication response";

        /// <summary>The assertion carrying the client data, authenticator data and signature.</summary>
        public WebAuthnAssertionResponse Response { get; }

        /// <summary>
        /// Initializes the authentication response.
        /// </summary>
        /// <param name="id">The base64url credential id. This is the value to look the stored credential up by.</param>
        /// <param name="rawId">The base64url credential id again, which must stand for the same bytes as <paramref name="id"/>.</param>
        /// <param name="type">The credential type, which must be <c>public-key</c>.</param>
        /// <param name="response">The assertion response.</param>
        /// <param name="authenticatorAttachment">How the authenticator was attached, or null.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="ArgumentException">
        /// An id is empty, too long or not base64url, the two ids stand for different bytes, or the type is
        /// not <c>public-key</c>.
        /// </exception>
        public WebAuthnAuthenticationResponse(
            string id,
            string rawId,
            string type,
            WebAuthnAssertionResponse response,
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

            using JsonDocument document = WebAuthnResponseReader.ParseObject(json, DocumentName);
            return ReadObject(document.RootElement);
        }

        /// <summary>
        /// Reads an authentication response nested in a larger document, for its System.Text.Json converter, by
        /// the same rules as <see cref="FromJson"/>.
        /// </summary>
        /// <param name="reader">The reader, on the first token of the value.</param>
        /// <returns>The parsed response.</returns>
        /// <exception cref="JsonException">The value is not an authentication response; the message is the parser's.</exception>
        internal static WebAuthnAuthenticationResponse Read(ref Utf8JsonReader reader)
        {
            return WebAuthnResponseReader.ReadNested(ref reader, DocumentName, ReadObject);
        }

        private static WebAuthnAuthenticationResponse ReadObject(JsonElement root)
        {
            JsonElement responseElement = WebAuthnResponseReader.ReadRequiredObject(root, ResponseFieldName, DocumentName);
            string responseSubject = $"{DocumentName}'s \"{ResponseFieldName}\"";

            WebAuthnAssertionResponse response = new WebAuthnAssertionResponse(
                WebAuthnResponseReader.ReadRequiredString(responseElement, WebAuthnAssertionResponse.ClientDataJsonFieldName, responseSubject),
                WebAuthnResponseReader.ReadRequiredString(responseElement, WebAuthnAssertionResponse.AuthenticatorDataFieldName, responseSubject),
                WebAuthnResponseReader.ReadRequiredString(responseElement, WebAuthnAssertionResponse.SignatureFieldName, responseSubject),
                WebAuthnResponseReader.ReadOptionalString(responseElement, WebAuthnAssertionResponse.UserHandleFieldName, responseSubject));

            return new WebAuthnAuthenticationResponse(
                WebAuthnResponseReader.ReadRequiredString(root, IdFieldName, DocumentName),
                WebAuthnResponseReader.ReadRequiredString(root, RawIdFieldName, DocumentName),
                WebAuthnResponseReader.ReadRequiredString(root, TypeFieldName, DocumentName),
                response,
                WebAuthnResponseReader.ReadOptionalString(root, AuthenticatorAttachmentFieldName, DocumentName));
        }
    }
}
