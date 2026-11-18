using System.Text;
using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The response types bound from a property of an application's request model read through the same parser as
    /// <c>FromJson</c>, with the same rules and the same size bound.
    /// </summary>
    [TestClass]
    public class WebAuthnResponseJsonConverterTests
    {
        private static readonly JsonSerializerOptions WebOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        private static string Envelope(string credentialJson)
        {
            return $"{{\"ceremonyNote\":\"sign-in\",\"webAuthnPayload\":{credentialJson}}}";
        }

        private static WebAuthnRegistrationResponse BindRegistration(string credentialJson)
        {
            WebAuthnJsonEnvelope<WebAuthnRegistrationResponse>? envelope =
                JsonSerializer.Deserialize<WebAuthnJsonEnvelope<WebAuthnRegistrationResponse>>(Envelope(credentialJson), WebOptions);
            return envelope!.WebAuthnPayload!;
        }

        private static WebAuthnAuthenticationResponse BindAuthentication(string credentialJson)
        {
            WebAuthnJsonEnvelope<WebAuthnAuthenticationResponse>? envelope =
                JsonSerializer.Deserialize<WebAuthnJsonEnvelope<WebAuthnAuthenticationResponse>>(Envelope(credentialJson), WebOptions);
            return envelope!.WebAuthnPayload!;
        }

        /// <summary>
        /// Pads a credential to exactly <paramref name="utf8ByteCount"/> bytes of UTF-8 with a field the parser
        /// ignores, inside the response member, so the size is the only thing that can reject it. Two-byte
        /// characters, so the text is far fewer characters than bytes, and a bound counted in characters passes it.
        /// </summary>
        private static string PadToUtf8Length(string json, int utf8ByteCount)
        {
            const string paddingField = ",\"padding\":\"\"";
            int paddingBytes = utf8ByteCount - Encoding.UTF8.GetByteCount(json) - paddingField.Length;

            if (paddingBytes < 0)
            {
                throw new InvalidOperationException($"The fixture JSON is already longer than {utf8ByteCount} bytes.");
            }

            string padding = new string('é', paddingBytes / 2) + new string('a', paddingBytes % 2);
            string padded = json.Insert(json.Length - 2, paddingField.Insert(paddingField.Length - 1, padding));

            Assert.AreEqual(utf8ByteCount, Encoding.UTF8.GetByteCount(padded));
            Assert.IsTrue(padded.Length < WebAuthnResponseReader.MaximumResponseLength, "the bound must be what counts bytes, not characters");
            return padded;
        }

        private static async Task<WebAuthnRegistrationResponse> BindRegistrationFromStreamAsync(string credentialJson)
        {
            // A small buffer, as a request body arrives, so the converter reads a value the serializer had to buffer.
            JsonSerializerOptions options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultBufferSize = 16 };
            using MemoryStream body = new MemoryStream(Encoding.UTF8.GetBytes(Envelope(credentialJson)));

            WebAuthnJsonEnvelope<WebAuthnRegistrationResponse>? envelope =
                await JsonSerializer.DeserializeAsync<WebAuthnJsonEnvelope<WebAuthnRegistrationResponse>>(body, options);
            return envelope!.WebAuthnPayload!;
        }

        [TestMethod]
        public void RegistrationResponse_BoundFromAModel_EqualsFromJson()
        {
            string json = WebAuthnResponseTestData.BrowserJson();

            WebAuthnRegistrationResponse bound = BindRegistration(json);

            Assert.AreEqual(WebAuthnRegistrationResponse.FromJson(json).ToJson(), bound.ToJson());
        }

        [TestMethod]
        public void AuthenticationResponse_BoundFromAModel_EqualsFromJson()
        {
            string json = WebAuthnAssertionTestData.BrowserJson();

            WebAuthnAuthenticationResponse bound = BindAuthentication(json);

            Assert.AreEqual(WebAuthnAuthenticationResponse.FromJson(json).ToJson(), bound.ToJson());
        }

        [TestMethod]
        public void Responses_WrittenInAModel_WriteExactlyToJson()
        {
            WebAuthnRegistrationResponse registration = WebAuthnRegistrationResponse.FromJson(WebAuthnResponseTestData.BrowserJson());
            WebAuthnAuthenticationResponse authentication = WebAuthnAuthenticationResponse.FromJson(WebAuthnAssertionTestData.MinimalBrowserJson());
            JsonSerializerOptions indented = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

            string registrationHost = JsonSerializer.Serialize(new WebAuthnJsonEnvelope<WebAuthnRegistrationResponse> { WebAuthnPayload = registration }, indented);
            string authenticationHost = JsonSerializer.Serialize(new WebAuthnJsonEnvelope<WebAuthnAuthenticationResponse> { WebAuthnPayload = authentication }, indented);

            using JsonDocument registrationDocument = JsonDocument.Parse(registrationHost);
            using JsonDocument authenticationDocument = JsonDocument.Parse(authenticationHost);
            Assert.AreEqual(registration.ToJson(), registrationDocument.RootElement.GetProperty("webAuthnPayload").GetRawText());
            Assert.AreEqual(authentication.ToJson(), authenticationDocument.RootElement.GetProperty("webAuthnPayload").GetRawText());
        }

        private static IEnumerable<object[]> MalformedRegistrations()
        {
            string browserJson = WebAuthnResponseTestData.BrowserJson();
            yield return new object[] { "missing clientDataJSON", browserJson.Replace($"\"{WebAuthnAttestationResponse.ClientDataJsonFieldName}\"", "\"clientDataJs0n\"") };
            yield return new object[] { "id and rawId disagree", browserJson.Replace($"\"rawId\":\"{WebAuthnResponseTestData.CredentialId}\"", $"\"rawId\":\"{WebAuthnResponseTestData.OtherCredentialId}\"") };
            yield return new object[] { "not public-key", browserJson.Replace("\"public-key\"", "\"password-key\"") };
            yield return new object[] { "id not base64url", browserJson.Replace($"\"id\":\"{WebAuthnResponseTestData.CredentialId}\"", "\"id\":\"not base64url!\"") };
            yield return new object[] { "an array", "[1,2,3]" };

            // A bare value is one token, so the measuring skip does not move past it: the size taken is the token's own.
            yield return new object[] { "a bare string", "\"a credential\"" };
            yield return new object[] { "a bare number", "42" };
        }

        [TestMethod]
        public void RegistrationResponse_NullOrMissingInAModel_BindsAsNull()
        {
            // Neither reaches the converter, so neither is a 400 from model binding: the endpoint checks for null.
            WebAuthnJsonEnvelope<WebAuthnRegistrationResponse>? withNull =
                JsonSerializer.Deserialize<WebAuthnJsonEnvelope<WebAuthnRegistrationResponse>>(Envelope("null"), WebOptions);
            WebAuthnJsonEnvelope<WebAuthnRegistrationResponse>? withoutMember =
                JsonSerializer.Deserialize<WebAuthnJsonEnvelope<WebAuthnRegistrationResponse>>("{\"ceremonyNote\":\"sign-in\"}", WebOptions);

            Assert.IsNull(withNull!.WebAuthnPayload);
            Assert.IsNull(withoutMember!.WebAuthnPayload);
        }

        [DataTestMethod]
        [DynamicData(nameof(MalformedRegistrations), DynamicDataSourceType.Method)]
        public void RegistrationResponse_MalformedInAModel_ThrowsJsonExceptionWithTheParsersMessage(string caseName, string credentialJson)
        {
            ArgumentException parserException = Assert.ThrowsException<ArgumentException>(() => WebAuthnRegistrationResponse.FromJson(credentialJson), caseName);

            JsonException exception = Assert.ThrowsException<JsonException>(() => BindRegistration(credentialJson), caseName);

            Assert.AreEqual(parserException.Message, exception.Message, caseName);
        }

        [TestMethod]
        public void AuthenticationResponse_MalformedInAModel_ThrowsJsonExceptionWithTheParsersMessage()
        {
            string credentialJson = WebAuthnAssertionTestData.BrowserJson().Replace($"\"{WebAuthnAssertionResponse.SignatureFieldName}\"", "\"sig\"");
            ArgumentException parserException = Assert.ThrowsException<ArgumentException>(() => WebAuthnAuthenticationResponse.FromJson(credentialJson));

            JsonException exception = Assert.ThrowsException<JsonException>(() => BindAuthentication(credentialJson));

            Assert.AreEqual(parserException.Message, exception.Message);
        }

        [TestMethod]
        public void RegistrationResponse_NestedJustOverTheBound_IsRefused()
        {
            string credentialJson = PadToUtf8Length(WebAuthnResponseTestData.BrowserJson(), WebAuthnResponseReader.MaximumResponseLength + 1);

            JsonException exception = Assert.ThrowsException<JsonException>(() => BindRegistration(credentialJson));

            StringAssert.Contains(exception.Message, $"{WebAuthnResponseReader.MaximumResponseLength + 1} bytes of UTF-8");
        }

        [TestMethod]
        public void RegistrationResponse_NestedAtTheBound_IsAccepted()
        {
            string credentialJson = PadToUtf8Length(WebAuthnResponseTestData.BrowserJson(), WebAuthnResponseReader.MaximumResponseLength);

            Assert.AreEqual(WebAuthnResponseTestData.CredentialId, BindRegistration(credentialJson).Id);
        }

        [TestMethod]
        public async Task RegistrationResponse_NestedJustOverTheBoundInAStreamedBody_IsRefused()
        {
            string credentialJson = PadToUtf8Length(WebAuthnResponseTestData.BrowserJson(), WebAuthnResponseReader.MaximumResponseLength + 1);

            await Assert.ThrowsExceptionAsync<JsonException>(() => BindRegistrationFromStreamAsync(credentialJson));
        }

        [TestMethod]
        public async Task RegistrationResponse_NestedAtTheBoundInAStreamedBody_IsAccepted()
        {
            string credentialJson = PadToUtf8Length(WebAuthnResponseTestData.BrowserJson(), WebAuthnResponseReader.MaximumResponseLength);

            WebAuthnRegistrationResponse bound = await BindRegistrationFromStreamAsync(credentialJson);

            Assert.AreEqual(WebAuthnResponseTestData.CredentialId, bound.Id);
        }

        [TestMethod]
        public void FromJson_OverTheBoundInUtf8BytesButNotInCharacters_Throws()
        {
            string json = PadToUtf8Length(WebAuthnResponseTestData.BrowserJson(), WebAuthnResponseReader.MaximumResponseLength + 1);

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => WebAuthnRegistrationResponse.FromJson(json));

            StringAssert.Contains(exception.Message, $"{WebAuthnResponseReader.MaximumResponseLength + 1} bytes of UTF-8");
        }

        [TestMethod]
        public void FromJson_AtTheBoundInUtf8Bytes_IsAccepted()
        {
            string json = PadToUtf8Length(WebAuthnResponseTestData.BrowserJson(), WebAuthnResponseReader.MaximumResponseLength);

            Assert.AreEqual(WebAuthnResponseTestData.CredentialId, WebAuthnRegistrationResponse.FromJson(json).Id);
        }
    }
}
