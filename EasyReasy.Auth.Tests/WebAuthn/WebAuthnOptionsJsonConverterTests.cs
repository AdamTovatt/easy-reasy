using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The options types nested in an application's response model write exactly their <c>ToJson</c> bytes, whatever
    /// serializer options the host uses, and are never read back.
    /// </summary>
    [TestClass]
    public class WebAuthnOptionsJsonConverterTests
    {
        private const string PayloadPropertyName = "WebAuthnPayload";

        /// <summary>
        /// Host options that would change the nested WebAuthn value if they reached it.
        /// </summary>
        /// <remarks>
        /// The naming policy, null handling and enum converter are what an application commonly sets, but every
        /// WebAuthn member pins its name, its null handling and its enum spelling, so on their own they cannot show a
        /// leak. Ignoring read-only properties (every WebAuthn property is get-only) and writing numbers as strings
        /// (<c>alg</c>) can, so a converter that let the host's options through fails here.
        /// </remarks>
        private static JsonSerializerOptions HostileHostOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
                IgnoreReadOnlyProperties = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString,
            };
        }

        private static IEnumerable<object[]> HostOptionsCases()
        {
            yield return new object[] { "web defaults", new JsonSerializerOptions(JsonSerializerDefaults.Web) };
            yield return new object[] { "hostile: snake_case, nulls written, enum converter, read-only ignored, numbers as strings", HostileHostOptions() };
            yield return new object[] { "indented", new JsonSerializerOptions { WriteIndented = true } };
        }

        private static PublicKeyCredentialCreationOptions NewCreationOptions()
        {
            // No excluded credentials, so excludeCredentials is null and a host writing nulls would show it.
            return WebAuthnTestData.NewGenerator().CreateRegistrationOptions(
                WebAuthnOptionsTestData.NewUser(),
                WebAuthnUserVerificationRequirement.Preferred,
                excludeCredentials: Array.Empty<WebAuthnCredentialReference>());
        }

        private static PublicKeyCredentialRequestOptions NewRequestOptions()
        {
            // One descriptor without transports, whose null a host writing nulls would show.
            return WebAuthnTestData.NewGenerator().CreateAuthenticationOptions(
                new[]
                {
                    new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId, new[] { "internal", "hybrid" }),
                    new WebAuthnCredentialReference(WebAuthnOptionsTestData.SecondCredentialId, null),
                },
                WebAuthnUserVerificationRequirement.Discouraged);
        }

        private static string NestedRawText(string hostJson)
        {
            using JsonDocument document = JsonDocument.Parse(hostJson);
            // The host's naming policy renames the model's own members, so the payload is found as the one that is not the note.
            JsonProperty payload = document.RootElement.EnumerateObject()
                .Single(property => !property.Name.Contains("note", StringComparison.OrdinalIgnoreCase));
            return payload.Value.GetRawText();
        }

        [DataTestMethod]
        [DynamicData(nameof(HostOptionsCases), DynamicDataSourceType.Method)]
        public void CreationOptions_NestedInAModel_WriteExactlyToJson(string caseName, JsonSerializerOptions hostOptions)
        {
            PublicKeyCredentialCreationOptions options = NewCreationOptions();

            string hostJson = JsonSerializer.Serialize(new WebAuthnJsonEnvelope<PublicKeyCredentialCreationOptions> { WebAuthnPayload = options }, hostOptions);

            Assert.AreEqual(options.ToJson(), NestedRawText(hostJson), caseName);
        }

        [DataTestMethod]
        [DynamicData(nameof(HostOptionsCases), DynamicDataSourceType.Method)]
        public void RequestOptions_NestedInAModel_WriteExactlyToJson(string caseName, JsonSerializerOptions hostOptions)
        {
            PublicKeyCredentialRequestOptions options = NewRequestOptions();

            string hostJson = JsonSerializer.Serialize(new WebAuthnJsonEnvelope<PublicKeyCredentialRequestOptions> { WebAuthnPayload = options }, hostOptions);

            Assert.AreEqual(options.ToJson(), NestedRawText(hostJson), caseName);
        }

        [TestMethod]
        public void Options_NestedInAnIndentedModel_StayACompactIslandInsideIndentedOutput()
        {
            PublicKeyCredentialRequestOptions options = NewRequestOptions();

            string hostJson = JsonSerializer.Serialize(
                new WebAuthnJsonEnvelope<PublicKeyCredentialRequestOptions> { WebAuthnPayload = options },
                new JsonSerializerOptions { WriteIndented = true });

            StringAssert.Contains(hostJson, options.ToJson());
            StringAssert.Contains(hostJson, "  \"CeremonyNote\"", "the host's own members stay indented");
        }

        [TestMethod]
        [DoNotParallelize]   // JsonSerializerSettings.CurrentOptions is global
        public void ToJson_WithChangedCurrentOptions_IsUnchanged()
        {
            PublicKeyCredentialCreationOptions creationOptions = NewCreationOptions();
            PublicKeyCredentialRequestOptions requestOptions = NewRequestOptions();
            WebAuthnRegistrationResponse registration = WebAuthnRegistrationResponse.FromJson(WebAuthnResponseTestData.BrowserJson());
            WebAuthnAuthenticationResponse authentication = WebAuthnAuthenticationResponse.FromJson(WebAuthnAssertionTestData.BrowserJson());
            string creationBefore = creationOptions.ToJson();
            string requestBefore = requestOptions.ToJson();
            string registrationBefore = registration.ToJson();
            string authenticationBefore = authentication.ToJson();

            JsonSerializerOptions originalOptions = JsonSerializerSettings.CurrentOptions;
            try
            {
                JsonSerializerOptions changedOptions = HostileHostOptions();
                changedOptions.WriteIndented = true;
                JsonSerializerSettings.CurrentOptions = changedOptions;

                Assert.AreEqual(creationBefore, creationOptions.ToJson(), nameof(PublicKeyCredentialCreationOptions));
                Assert.AreEqual(requestBefore, requestOptions.ToJson(), nameof(PublicKeyCredentialRequestOptions));
                Assert.AreEqual(registrationBefore, registration.ToJson(), nameof(WebAuthnRegistrationResponse));
                Assert.AreEqual(authenticationBefore, authentication.ToJson(), nameof(WebAuthnAuthenticationResponse));
            }
            finally
            {
                JsonSerializerSettings.CurrentOptions = originalOptions;
            }
        }

        [TestMethod]
        public void CreationOptions_ReadFromAModel_ThrowsNotSupportedNamingTheType()
        {
            NotSupportedException exception = Assert.ThrowsException<NotSupportedException>(
                () => JsonSerializer.Deserialize<WebAuthnJsonEnvelope<PublicKeyCredentialCreationOptions>>($"{{\"{PayloadPropertyName}\":{NewCreationOptions().ToJson()}}}"));

            StringAssert.Contains(exception.Message, nameof(PublicKeyCredentialCreationOptions));
        }

        [TestMethod]
        public void RequestOptions_ReadFromAModel_ThrowsNotSupportedNamingTheType()
        {
            NotSupportedException exception = Assert.ThrowsException<NotSupportedException>(
                () => JsonSerializer.Deserialize<WebAuthnJsonEnvelope<PublicKeyCredentialRequestOptions>>($"{{\"{PayloadPropertyName}\":{NewRequestOptions().ToJson()}}}"));

            StringAssert.Contains(exception.Message, nameof(PublicKeyCredentialRequestOptions));
        }
    }
}
