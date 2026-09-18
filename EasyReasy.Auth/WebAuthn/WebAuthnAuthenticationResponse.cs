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
    /// </remarks>
    public sealed class WebAuthnAuthenticationResponse : WebAuthnCredentialResponse
    {
        /// <summary>The assertion carrying the client data, authenticator data and signature.</summary>
        [JsonPropertyName(ResponseFieldName)]
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

        /// <inheritdoc />
        public override string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonSerializerSettings.CurrentOptions);
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

            using JsonDocument document = WebAuthnResponseReader.ParseObject(json, what);
            JsonElement root = document.RootElement;

            JsonElement responseElement = WebAuthnResponseReader.ReadRequiredObject(root, ResponseFieldName, what);
            string responseSubject = $"{what}'s \"{ResponseFieldName}\"";

            WebAuthnAssertionResponse response = new WebAuthnAssertionResponse(
                WebAuthnResponseReader.ReadRequiredString(responseElement, WebAuthnAssertionResponse.ClientDataJsonFieldName, responseSubject),
                WebAuthnResponseReader.ReadRequiredString(responseElement, WebAuthnAssertionResponse.AuthenticatorDataFieldName, responseSubject),
                WebAuthnResponseReader.ReadRequiredString(responseElement, WebAuthnAssertionResponse.SignatureFieldName, responseSubject),
                WebAuthnResponseReader.ReadOptionalString(responseElement, WebAuthnAssertionResponse.UserHandleFieldName, responseSubject));

            return new WebAuthnAuthenticationResponse(
                WebAuthnResponseReader.ReadRequiredString(root, IdFieldName, what),
                WebAuthnResponseReader.ReadRequiredString(root, RawIdFieldName, what),
                WebAuthnResponseReader.ReadRequiredString(root, TypeFieldName, what),
                response,
                WebAuthnResponseReader.ReadOptionalString(root, AuthenticatorAttachmentFieldName, what));
        }
    }
}
