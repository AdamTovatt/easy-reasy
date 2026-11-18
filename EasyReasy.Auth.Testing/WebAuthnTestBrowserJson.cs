using System.Buffers.Text;
using System.Text.Json.Nodes;

namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// Writes the body a page posts: what the browser's <c>PublicKeyCredential.toJSON()</c> writes, which
    /// is EasyReasy.Auth's own serialization of the response plus the members the browser adds that the
    /// typed responses do not carry.
    /// </summary>
    internal static class WebAuthnTestBrowserJson
    {
        private const string ClientExtensionResultsFieldName = "clientExtensionResults";

        /// <summary>
        /// Writes a registration's <c>RegistrationResponseJSON</c> (WebAuthn Level 3 §5.1): the response,
        /// with the attestation's <c>authenticatorData</c>, <c>publicKey</c> and <c>publicKeyAlgorithm</c>
        /// and empty <c>clientExtensionResults</c>.
        /// </summary>
        /// <param name="response">The typed registration.</param>
        /// <param name="authenticatorData">The authenticator data inside the attestation object.</param>
        /// <param name="subjectPublicKeyInfo">The DER SubjectPublicKeyInfo of the enrolled public key.</param>
        /// <param name="algorithm">The algorithm the enrolled key signs with.</param>
        public static string ForRegistration(
            WebAuthnRegistrationResponse response,
            byte[] authenticatorData,
            byte[] subjectPublicKeyInfo,
            CoseAlgorithm algorithm)
        {
            JsonObject body = AsObject(JsonNode.Parse(response.ToJson()));

            JsonObject attestation = AsObject(body[WebAuthnCredentialResponse.ResponseFieldName]);
            attestation["authenticatorData"] = Base64Url.EncodeToString(authenticatorData);
            attestation["publicKey"] = Base64Url.EncodeToString(subjectPublicKeyInfo);
            attestation["publicKeyAlgorithm"] = (int)algorithm;
            body[ClientExtensionResultsFieldName] = new JsonObject();

            return body.ToJsonString();
        }

        /// <summary>
        /// Writes an assertion's <c>AuthenticationResponseJSON</c> (WebAuthn Level 3 §5.1): the response,
        /// with empty <c>clientExtensionResults</c>.
        /// </summary>
        /// <param name="response">The typed assertion.</param>
        public static string ForAssertion(WebAuthnAuthenticationResponse response)
        {
            JsonObject body = AsObject(JsonNode.Parse(response.ToJson()));
            body[ClientExtensionResultsFieldName] = new JsonObject();

            return body.ToJsonString();
        }

        /// <summary>
        /// Reads a node of the JSON EasyReasy.Auth's <c>ToJson()</c> wrote as the object it always is.
        /// </summary>
        private static JsonObject AsObject(JsonNode? node)
        {
            return node as JsonObject
                ?? throw new InvalidOperationException("EasyReasy.Auth serialized a response member that is not a JSON object.");
        }
    }
}
