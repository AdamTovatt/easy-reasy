using System.Text;
using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The JSON contract the browser writes and this library reads.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class WebAuthnRegistrationResponseTests : HostileNamingPolicyTestBase
    {
        [TestMethod]
        public void FromJson_BrowserResponse_ReadsEveryField()
        {
            WebAuthnRegistrationResponse response = WebAuthnRegistrationResponse.FromJson(WebAuthnResponseTestData.BrowserJson());

            Assert.AreEqual(WebAuthnResponseTestData.CredentialId, response.Id);
            Assert.AreEqual(WebAuthnResponseTestData.CredentialId, response.RawId);
            Assert.AreEqual(PublicKeyCredentialType.PublicKey, response.Type);
            Assert.AreEqual("platform", response.AuthenticatorAttachment);
            Assert.AreEqual(WebAuthnResponseTestData.ClientDataJson, response.Response.ClientDataJson);
            Assert.AreEqual(WebAuthnResponseTestData.AttestationObject, response.Response.AttestationObject);
            CollectionAssert.AreEqual(new[] { "internal", "hybrid" }, response.Response.Transports!.ToArray());
        }

        [TestMethod]
        public void FromJson_IdAndRawIdSpelledDifferently_KeepsEachInItsOwnField()
        {
            // A browser writes the same text in both, which makes every other test here blind to the two
            // being read the wrong way round. Padding gives the same bytes a second legal spelling, so the
            // response is still valid while the two fields are told apart.
            string json = WebAuthnResponseTestData.BrowserJson()
                .Replace(
                    $"\"{WebAuthnRegistrationResponse.RawIdFieldName}\":\"{WebAuthnResponseTestData.CredentialId}\"",
                    $"\"{WebAuthnRegistrationResponse.RawIdFieldName}\":\"{WebAuthnResponseTestData.CredentialId}=\"",
                    StringComparison.Ordinal);

            WebAuthnRegistrationResponse response = WebAuthnRegistrationResponse.FromJson(json);

            Assert.AreEqual(WebAuthnResponseTestData.CredentialId, response.Id);
            Assert.AreEqual(WebAuthnResponseTestData.CredentialId + "=", response.RawId);
        }

        [TestMethod]
        public void FromJson_DecodesTheBase64UrlFields()
        {
            WebAuthnRegistrationResponse response = WebAuthnRegistrationResponse.FromJson(WebAuthnResponseTestData.BrowserJson());

            Assert.AreEqual("{\"type\":\"webauthn.create\"}", Encoding.UTF8.GetString(response.Response.ClientDataJsonBytes));
            Assert.AreEqual("credential-one", Encoding.UTF8.GetString(response.CredentialIdBytes));
        }

        [TestMethod]
        public void FromJson_ResponseWithoutTheOptionalFields_IsAccepted()
        {
            string json = $"{{\"id\":\"{WebAuthnResponseTestData.CredentialId}\",\"rawId\":\"{WebAuthnResponseTestData.CredentialId}\"," +
                $"\"type\":\"public-key\",\"response\":{{\"clientDataJSON\":\"{WebAuthnResponseTestData.ClientDataJson}\"," +
                $"\"attestationObject\":\"{WebAuthnResponseTestData.AttestationObject}\"}}}}";

            WebAuthnRegistrationResponse response = WebAuthnRegistrationResponse.FromJson(json);

            Assert.IsNull(response.AuthenticatorAttachment);
            Assert.IsNull(response.Response.Transports);
        }

        [TestMethod]
        public void FromJson_AuthenticatorAttachmentExplicitlyNull_ReadsAsAbsent()
        {
            // The reader distinguishes absent from explicitly null, and only the first is what a browser
            // writes — so without this the null branch is never taken.
            string json = ReplaceFieldWithLiteral(WebAuthnResponseTestData.BrowserJson(), WebAuthnRegistrationResponse.AuthenticatorAttachmentFieldName, "null");

            Assert.IsNull(WebAuthnRegistrationResponse.FromJson(json).AuthenticatorAttachment);
        }

        [TestMethod]
        public void FromJson_TransportsExplicitlyNull_ReadsAsAbsent()
        {
            string json = ReplaceFieldWithLiteral(WebAuthnResponseTestData.BrowserJson(), WebAuthnAttestationResponse.TransportsFieldName, "null");

            Assert.IsNull(WebAuthnRegistrationResponse.FromJson(json).Response.Transports);
        }

        [TestMethod]
        public void FromJson_UnknownFields_AreIgnored()
        {
            // The browser emits clientExtensionResults and several convenience copies of what is inside the
            // attestation object; a response carrying them has to parse, not fail.
            string json = WebAuthnResponseTestData.BrowserJson()
                .Replace("\"clientExtensionResults\":{}", "\"clientExtensionResults\":{},\"publicKeyAlgorithm\":-7", StringComparison.Ordinal);

            Assert.AreEqual(WebAuthnResponseTestData.CredentialId, WebAuthnRegistrationResponse.FromJson(json).Id);
        }

        [DataTestMethod]
        [DataRow("id")]
        [DataRow("rawId")]
        [DataRow("type")]
        [DataRow("response")]
        [DataRow("clientDataJSON")]
        [DataRow("attestationObject")]
        public void FromJson_MissingRequiredField_NamesIt(string missingField)
        {
            string json = RemoveField(WebAuthnResponseTestData.BrowserJson(), missingField);

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => WebAuthnRegistrationResponse.FromJson(json));

            // Quoted, because the unquoted word "response" appears in the message's own subject and would
            // satisfy the assertion whichever field had gone missing.
            StringAssert.Contains(exception.Message, $"\"{missingField}\"");
        }

        [DataTestMethod]
        [DataRow("id")]
        [DataRow("type")]
        [DataRow("clientDataJSON")]
        public void FromJson_RequiredFieldPresentButEmpty_SaysItIsEmpty(string emptiedField)
        {
            // Present-and-empty is a third case, distinct from absent and from malformed, and each of
            // these fields would otherwise be rejected further down by whatever gives it meaning — with a
            // message about base64url or about "public-key" rather than about the field being blank.
            string json = EmptyField(WebAuthnResponseTestData.BrowserJson(), emptiedField);

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => WebAuthnRegistrationResponse.FromJson(json));

            StringAssert.Contains(exception.Message, $"has an empty \"{emptiedField}\"");
        }

        [TestMethod]
        public void FromJson_ResponseThatIsNotAnObject_Throws()
        {
            // Reading properties off a JSON string throws InvalidOperationException, which is not what an
            // endpoint parsing a request body is prepared to catch, so the kind is checked before it is read.
            string json = ReplaceFieldWithLiteral(WebAuthnResponseTestData.BrowserJson(), WebAuthnRegistrationResponse.ResponseFieldName, "\"nope\"");

            Assert.ThrowsException<ArgumentException>(() => WebAuthnRegistrationResponse.FromJson(json));
        }

        [TestMethod]
        public void FromJson_RequiredStringThatIsNotAString_Throws()
        {
            string json = ReplaceFieldWithLiteral(WebAuthnResponseTestData.BrowserJson(), WebAuthnRegistrationResponse.IdFieldName, "7");

            Assert.ThrowsException<ArgumentException>(() => WebAuthnRegistrationResponse.FromJson(json));
        }

        [TestMethod]
        public void FromJson_OptionalStringThatIsNotAString_Throws()
        {
            string json = ReplaceFieldWithLiteral(WebAuthnResponseTestData.BrowserJson(), WebAuthnRegistrationResponse.AuthenticatorAttachmentFieldName, "7");

            Assert.ThrowsException<ArgumentException>(() => WebAuthnRegistrationResponse.FromJson(json));
        }

        [TestMethod]
        public void FromJson_TransportsThatIsNotAnArray_Throws()
        {
            string json = ReplaceFieldWithLiteral(WebAuthnResponseTestData.BrowserJson(), WebAuthnAttestationResponse.TransportsFieldName, "\"internal\"");

            Assert.ThrowsException<ArgumentException>(() => WebAuthnRegistrationResponse.FromJson(json));
        }

        [TestMethod]
        public void FromJson_TransportsCarryingSomethingThatIsNotAString_Throws()
        {
            string json = ReplaceFieldWithLiteral(WebAuthnResponseTestData.BrowserJson(), WebAuthnAttestationResponse.TransportsFieldName, "[1,2]");

            Assert.ThrowsException<ArgumentException>(() => WebAuthnRegistrationResponse.FromJson(json));
        }

        [TestMethod]
        public void FromJson_NotJson_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => WebAuthnRegistrationResponse.FromJson("not json"));
        }

        [TestMethod]
        public void FromJson_JsonThatIsNotAnObject_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => WebAuthnRegistrationResponse.FromJson("[1,2,3]"));
        }

        [TestMethod]
        public void FromJson_Null_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => WebAuthnRegistrationResponse.FromJson(null!));
        }

        [TestMethod]
        public void FromJson_BodyLongerThanAnyCredential_Throws()
        {
            // Padded to one character over the bound while staying a response that would otherwise parse,
            // so the length is the only thing that can reject it. Filled with junk instead, it would be
            // caught as invalid JSON and this would pass with no bound in place at all.
            string json = PadToLength(WebAuthnResponseTestData.BrowserJson(), WebAuthnResponseReader.MaximumResponseLength + 1);

            Assert.ThrowsException<ArgumentException>(() => WebAuthnRegistrationResponse.FromJson(json));
        }

        [TestMethod]
        public void FromJson_BodyAtTheLengthLimit_IsAccepted()
        {
            // Pins which side of the bound is rejected; without it the cap could be off by one in the
            // direction that refuses legitimate responses and nothing would say so.
            string json = PadToLength(WebAuthnResponseTestData.BrowserJson(), WebAuthnResponseReader.MaximumResponseLength);

            Assert.AreEqual(WebAuthnResponseTestData.CredentialId, WebAuthnRegistrationResponse.FromJson(json).Id);
        }

        [TestMethod]
        public void Constructor_TypeThatIsNotPublicKey_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnRegistrationResponse(
                    WebAuthnResponseTestData.CredentialId,
                    WebAuthnResponseTestData.CredentialId,
                    "password-key",
                    NewAttestationResponse()));

            Assert.AreEqual("type", exception.ParamName);
        }

        [TestMethod]
        public void Constructor_IdAndRawIdStandingForDifferentBytes_Throws()
        {
            // The two are the same bytes in every response a browser writes, so a pair that disagrees would
            // otherwise leave a choice of which one the credential is.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnRegistrationResponse(
                    WebAuthnResponseTestData.CredentialId,
                    WebAuthnResponseTestData.OtherCredentialId,
                    PublicKeyCredentialType.PublicKey,
                    NewAttestationResponse()));

            Assert.AreEqual("rawId", exception.ParamName);
        }

        [TestMethod]
        public void Constructor_PaddedRawId_IsAcceptedAsTheSameCredentialId()
        {
            // Different text, same bytes: comparing the decoded values rather than the strings is what
            // keeps a client library's padding from reading as two different credentials.
            WebAuthnRegistrationResponse response = new WebAuthnRegistrationResponse(
                WebAuthnResponseTestData.CredentialId,
                WebAuthnResponseTestData.CredentialId + "=",
                PublicKeyCredentialType.PublicKey,
                NewAttestationResponse());

            Assert.AreEqual(WebAuthnResponseTestData.CredentialId + "=", response.RawId);
        }

        [TestMethod]
        public void Constructor_IdThatIsNotBase64Url_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnRegistrationResponse("not base64url!", "not base64url!", PublicKeyCredentialType.PublicKey, NewAttestationResponse()));

            Assert.AreEqual("id", exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        public void Constructor_IdThatStandsForNoBytes_Throws(string id)
        {
            // Whitespace is valid base64url and decodes to nothing, so without an explicit guard a blank
            // id would be accepted as a credential of zero bytes rather than rejected — and every later
            // ceremony would look for a credential that cannot exist.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnRegistrationResponse(id, id, PublicKeyCredentialType.PublicKey, NewAttestationResponse()));

            Assert.AreEqual("id", exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("id")]
        [DataRow("rawId")]
        [DataRow("type")]
        [DataRow("response")]
        public void Constructor_NullArgument_NamesIt(string nullArgumentName)
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => new WebAuthnRegistrationResponse(
                    nullArgumentName == "id" ? null! : WebAuthnResponseTestData.CredentialId,
                    nullArgumentName == "rawId" ? null! : WebAuthnResponseTestData.CredentialId,
                    nullArgumentName == "type" ? null! : PublicKeyCredentialType.PublicKey,
                    nullArgumentName == "response" ? null! : NewAttestationResponse()));

            Assert.AreEqual(nullArgumentName, exception.ParamName);
        }

        [TestMethod]
        public void ToJson_WritesTheFieldNamesTheBrowserUses()
        {
            using JsonDocument document = JsonDocument.Parse(WebAuthnRegistrationResponse.FromJson(WebAuthnResponseTestData.BrowserJson()).ToJson());

            CollectionAssert.AreEquivalent(
                new[] { "id", "rawId", "type", "response", "authenticatorAttachment" },
                document.RootElement.EnumerateObject().Select(property => property.Name).ToArray());

            CollectionAssert.AreEquivalent(
                new[] { "clientDataJSON", "attestationObject", "transports" },
                document.RootElement.GetProperty("response").EnumerateObject().Select(property => property.Name).ToArray());
        }

        [TestMethod]
        public void ToJson_WritesEveryValueUnderItsOwnName()
        {
            // The names test above passes just as well over a response whose values have been shuffled
            // between the right keys, which is what this closes.
            using JsonDocument document = JsonDocument.Parse(WebAuthnRegistrationResponse.FromJson(WebAuthnResponseTestData.BrowserJson()).ToJson());
            JsonElement root = document.RootElement;
            JsonElement response = root.GetProperty("response");

            Assert.AreEqual(WebAuthnResponseTestData.CredentialId, root.GetProperty("id").GetString());
            Assert.AreEqual(WebAuthnResponseTestData.CredentialId, root.GetProperty("rawId").GetString());
            Assert.AreEqual(PublicKeyCredentialType.PublicKey, root.GetProperty("type").GetString());
            Assert.AreEqual("platform", root.GetProperty("authenticatorAttachment").GetString());
            Assert.AreEqual(WebAuthnResponseTestData.ClientDataJson, response.GetProperty("clientDataJSON").GetString());
            Assert.AreEqual(WebAuthnResponseTestData.AttestationObject, response.GetProperty("attestationObject").GetString());
            CollectionAssert.AreEqual(
                new[] { "internal", "hybrid" },
                response.GetProperty("transports").EnumerateArray().Select(item => item.GetString()).ToArray());
        }

        [TestMethod]
        public void ToJson_OmitsTheOptionalFieldsThatWereAbsent()
        {
            string json = $"{{\"id\":\"{WebAuthnResponseTestData.CredentialId}\",\"rawId\":\"{WebAuthnResponseTestData.CredentialId}\"," +
                $"\"type\":\"public-key\",\"response\":{{\"clientDataJSON\":\"{WebAuthnResponseTestData.ClientDataJson}\"," +
                $"\"attestationObject\":\"{WebAuthnResponseTestData.AttestationObject}\"}}}}";

            string written = WebAuthnRegistrationResponse.FromJson(json).ToJson();

            Assert.IsFalse(written.Contains("authenticatorAttachment", StringComparison.Ordinal));
            Assert.IsFalse(written.Contains("transports", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ToString_ReturnsTheSameStringAsToJson()
        {
            WebAuthnRegistrationResponse response = WebAuthnRegistrationResponse.FromJson(WebAuthnResponseTestData.BrowserJson());

            Assert.AreEqual(response.ToJson(), response.ToString());
        }

        private static WebAuthnAttestationResponse NewAttestationResponse()
        {
            return new WebAuthnAttestationResponse(WebAuthnResponseTestData.ClientDataJson, WebAuthnResponseTestData.AttestationObject);
        }

        /// <summary>
        /// Grows a response to exactly <paramref name="length"/> characters by adding a field nothing
        /// reads, so it stays a response that would otherwise parse.
        /// </summary>
        private static string PadToLength(string json, int length)
        {
            const string paddingField = ",\"padding\":\"\"";
            int paddingLength = length - json.Length - paddingField.Length;

            if (paddingLength < 0)
            {
                throw new InvalidOperationException($"The fixture JSON is already longer than {length} characters.");
            }

            return json.Insert(json.Length - 2, paddingField.Insert(paddingField.Length - 1, new string('a', paddingLength)));
        }

        /// <summary>
        /// Renames a field so it is no longer the one being read, leaving the JSON otherwise intact.
        /// </summary>
        private static string RemoveField(string json, string fieldName)
        {
            return json.Replace($"\"{fieldName}\":", $"\"absent-{fieldName}\":", StringComparison.Ordinal);
        }

        /// <summary>
        /// Replaces a string field's value with the empty string, leaving the JSON otherwise intact.
        /// </summary>
        private static string EmptyField(string json, string fieldName)
        {
            string prefix = $"\"{fieldName}\":\"";
            int valueStart = json.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
            int valueEnd = json.IndexOf('"', valueStart);

            return json.Remove(valueStart, valueEnd - valueStart);
        }

        /// <summary>
        /// Replaces a field's whole value with the given JSON literal, so a field can be given a kind it
        /// should not have.
        /// </summary>
        private static string ReplaceFieldWithLiteral(string json, string fieldName, string literal)
        {
            string prefix = $"\"{fieldName}\":";
            int valueStart = json.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
            int valueEnd = FindValueEnd(json, valueStart);

            return json.Remove(valueStart, valueEnd - valueStart).Insert(valueStart, literal);
        }

        /// <summary>
        /// Finds where the JSON value starting at <paramref name="valueStart"/> ends, handling the string,
        /// array and object forms this fixture uses.
        /// </summary>
        private static int FindValueEnd(string json, int valueStart)
        {
            char opening = json[valueStart];

            if (opening == '"')
            {
                return json.IndexOf('"', valueStart + 1) + 1;
            }

            char closing = opening == '[' ? ']' : '}';
            int depth = 0;
            for (int index = valueStart; index < json.Length; index++)
            {
                if (json[index] == opening)
                {
                    depth++;
                }
                else if (json[index] == closing)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return index + 1;
                    }
                }
            }

            throw new InvalidOperationException($"The fixture JSON has no closing '{closing}' for the value at {valueStart}.");
        }
    }
}
