namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The hand-written browser assertion the authentication response-model tests are written against, the
    /// counterpart of <see cref="WebAuthnResponseTestData"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately not a verifiable assertion: these tests are about the JSON contract, and the encoded
    /// fields only have to be base64url of something. An assertion that also verifies is what
    /// <see cref="SyntheticAuthenticationCeremony"/> builds, and the two meet in one test that parses its
    /// JSON and verifies the result.
    /// </remarks>
    internal static class WebAuthnAssertionTestData
    {
        /// <summary>Base64url of <c>{"type":"webauthn.get"}</c>.</summary>
        public const string ClientDataJson = "eyJ0eXBlIjoid2ViYXV0aG4uZ2V0In0";

        /// <summary>Base64url of "authenticator-data".</summary>
        public const string AuthenticatorData = "YXV0aGVudGljYXRvci1kYXRh";

        /// <summary>Base64url of "signature".</summary>
        public const string Signature = "c2lnbmF0dXJl";

        /// <summary>Base64url of "user-handle".</summary>
        public const string UserHandle = "dXNlci1oYW5kbGU";

        /// <summary>
        /// An assertion with every field set, each to a distinct value.
        /// </summary>
        public static WebAuthnAssertionResponse NewAssertion()
        {
            return new WebAuthnAssertionResponse(ClientDataJson, AuthenticatorData, Signature, UserHandle);
        }

        /// <summary>
        /// The body a page posts back, with every field the browser writes.
        /// </summary>
        /// <param name="userHandle">The user handle to write.</param>
        /// <param name="quoteUserHandle">Whether to write it as a JSON string, for spelling a literal null.</param>
        public static string BrowserJson(string userHandle = UserHandle, bool quoteUserHandle = true)
        {
            string writtenUserHandle = quoteUserHandle ? $"\"{userHandle}\"" : userHandle;

            return $"{{\"{WebAuthnAuthenticationResponse.IdFieldName}\":\"{WebAuthnResponseTestData.CredentialId}\"," +
                $"\"{WebAuthnAuthenticationResponse.RawIdFieldName}\":\"{WebAuthnResponseTestData.CredentialId}\"," +
                $"\"{WebAuthnAuthenticationResponse.TypeFieldName}\":\"{PublicKeyCredentialType.PublicKey}\"," +
                $"\"{WebAuthnAuthenticationResponse.AuthenticatorAttachmentFieldName}\":\"platform\"," +
                "\"clientExtensionResults\":{}," +
                $"\"{WebAuthnAuthenticationResponse.ResponseFieldName}\":{{" +
                $"\"{WebAuthnAssertionResponse.ClientDataJsonFieldName}\":\"{ClientDataJson}\"," +
                $"\"{WebAuthnAssertionResponse.AuthenticatorDataFieldName}\":\"{AuthenticatorData}\"," +
                $"\"{WebAuthnAssertionResponse.SignatureFieldName}\":\"{Signature}\"," +
                $"\"{WebAuthnAssertionResponse.UserHandleFieldName}\":{writtenUserHandle}}}}}";
        }

        /// <summary>
        /// The body a page posts back with neither optional field — what a browser writes when the
        /// authenticator sent no user handle and reported no attachment.
        /// </summary>
        public static string MinimalBrowserJson()
        {
            return $"{{\"{WebAuthnAuthenticationResponse.IdFieldName}\":\"{WebAuthnResponseTestData.CredentialId}\"," +
                $"\"{WebAuthnAuthenticationResponse.RawIdFieldName}\":\"{WebAuthnResponseTestData.CredentialId}\"," +
                $"\"{WebAuthnAuthenticationResponse.TypeFieldName}\":\"{PublicKeyCredentialType.PublicKey}\"," +
                $"\"{WebAuthnAuthenticationResponse.ResponseFieldName}\":{{" +
                $"\"{WebAuthnAssertionResponse.ClientDataJsonFieldName}\":\"{ClientDataJson}\"," +
                $"\"{WebAuthnAssertionResponse.AuthenticatorDataFieldName}\":\"{AuthenticatorData}\"," +
                $"\"{WebAuthnAssertionResponse.SignatureFieldName}\":\"{Signature}\"}}}}";
        }
    }
}
