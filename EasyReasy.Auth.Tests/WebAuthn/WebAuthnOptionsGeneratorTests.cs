using System.Buffers.Text;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class WebAuthnOptionsGeneratorTests
    {
        [TestMethod]
        public void CreateRegistrationOptions_NamesTheRelyingPartyItWasConfiguredWith()
        {
            PublicKeyCredentialCreationOptions options = WebAuthnOptionsTestData.NewGenerator()
                .CreateRegistrationOptions(WebAuthnOptionsTestData.NewUser(), WebAuthnUserVerificationRequirement.Required);

            Assert.AreEqual("example.com", options.RelyingParty.Id);
            Assert.AreEqual("Contoso", options.RelyingParty.Name);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_NamesTheRelyingPartyIdItWasConfiguredWith()
        {
            PublicKeyCredentialRequestOptions options = WebAuthnOptionsTestData.NewGenerator()
                .CreateAuthenticationOptions(new[] { WebAuthnOptionsTestData.FirstCredentialId }, WebAuthnUserVerificationRequirement.Required);

            Assert.AreEqual("example.com", options.RelyingPartyId);
        }

        [TestMethod]
        public void CreateRegistrationOptions_ChallengeIsThirtyTwoBytesOfBase64Url()
        {
            PublicKeyCredentialCreationOptions options = WebAuthnOptionsTestData.NewGenerator()
                .CreateRegistrationOptions(WebAuthnOptionsTestData.NewUser(), WebAuthnUserVerificationRequirement.Required);

            Assert.AreEqual(32, Base64Url.DecodeFromChars(options.Challenge).Length);
            Assert.IsTrue(Base64UrlEncoding.IsCanonical(options.Challenge));
        }

        [TestMethod]
        public void CreateAuthenticationOptions_ChallengeIsThirtyTwoBytesOfBase64Url()
        {
            PublicKeyCredentialRequestOptions options = WebAuthnOptionsTestData.NewGenerator()
                .CreateAuthenticationOptions(new[] { WebAuthnOptionsTestData.FirstCredentialId }, WebAuthnUserVerificationRequirement.Required);

            Assert.AreEqual(32, Base64Url.DecodeFromChars(options.Challenge).Length);
            Assert.IsTrue(Base64UrlEncoding.IsCanonical(options.Challenge));
        }

        [TestMethod]
        public void CreateRegistrationOptions_CalledTwice_IssuesDifferentChallenges()
        {
            WebAuthnOptionsGenerator generator = WebAuthnOptionsTestData.NewGenerator();

            string first = generator.CreateRegistrationOptions(WebAuthnOptionsTestData.NewUser(), WebAuthnUserVerificationRequirement.Required).Challenge;
            string second = generator.CreateRegistrationOptions(WebAuthnOptionsTestData.NewUser(), WebAuthnUserVerificationRequirement.Required).Challenge;

            Assert.AreNotEqual(first, second);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_CalledTwice_IssuesDifferentChallenges()
        {
            WebAuthnOptionsGenerator generator = WebAuthnOptionsTestData.NewGenerator();

            string first = generator.CreateAuthenticationOptions(new[] { WebAuthnOptionsTestData.FirstCredentialId }, WebAuthnUserVerificationRequirement.Required).Challenge;
            string second = generator.CreateAuthenticationOptions(new[] { WebAuthnOptionsTestData.FirstCredentialId }, WebAuthnUserVerificationRequirement.Required).Challenge;

            Assert.AreNotEqual(first, second);
        }

        [TestMethod]
        public void CreateRegistrationOptions_OffersEs256ThenRs256AndNothingElse()
        {
            // The numbers are the IANA COSE identifiers, written out rather than read from CoseAlgorithm:
            // referring to the enum here would make the test agree with whatever the enum said.
            PublicKeyCredentialCreationOptions options = WebAuthnOptionsTestData.NewGenerator()
                .CreateRegistrationOptions(WebAuthnOptionsTestData.NewUser(), WebAuthnUserVerificationRequirement.Required);

            CollectionAssert.AreEqual(new[] { -7, -257 }, options.AcceptedAlgorithms.Select(parameters => parameters.Algorithm).ToArray());
            CollectionAssert.AreEqual(new[] { "public-key", "public-key" }, options.AcceptedAlgorithms.Select(parameters => parameters.Type).ToArray());
        }

        [TestMethod]
        public void CreateRegistrationOptions_AcceptedAlgorithms_CannotBeRewrittenThroughTheReturnedList()
        {
            // The list is shared by every options object this generator produces, so a caller who could
            // cast it back to an array would be editing what every later registration offers.
            PublicKeyCredentialCreationOptions options = WebAuthnOptionsTestData.NewGenerator()
                .CreateRegistrationOptions(WebAuthnOptionsTestData.NewUser(), WebAuthnUserVerificationRequirement.Required);

            Assert.IsNull(options.AcceptedAlgorithms as PublicKeyCredentialParameters[]);
        }

        [TestMethod]
        public void CreateRegistrationOptions_AsksForNoAttestation()
        {
            PublicKeyCredentialCreationOptions options = WebAuthnOptionsTestData.NewGenerator()
                .CreateRegistrationOptions(WebAuthnOptionsTestData.NewUser(), WebAuthnUserVerificationRequirement.Required);

            Assert.AreEqual("none", options.Attestation);
        }

        [TestMethod]
        public void CreateRegistrationOptions_AsksForNoDiscoverableCredential()
        {
            PublicKeyCredentialCreationOptions options = WebAuthnOptionsTestData.NewGenerator()
                .CreateRegistrationOptions(WebAuthnOptionsTestData.NewUser(), WebAuthnUserVerificationRequirement.Required);

            Assert.AreEqual("discouraged", options.AuthenticatorSelection.ResidentKey);
            Assert.IsFalse(options.AuthenticatorSelection.RequireResidentKey);
        }

        [TestMethod]
        public void CreateRegistrationOptions_NoExcludedCredentials_LeavesTheFieldOutOfTheJson()
        {
            PublicKeyCredentialCreationOptions options = WebAuthnOptionsTestData.NewGenerator()
                .CreateRegistrationOptions(WebAuthnOptionsTestData.NewUser(), WebAuthnUserVerificationRequirement.Required);

            Assert.IsNull(options.ExcludeCredentials);
            Assert.IsFalse(options.ToJson().Contains("excludeCredentials", StringComparison.Ordinal));
        }

        [TestMethod]
        public void CreateRegistrationOptions_EmptyExcludedCredentials_LeavesTheFieldOutOfTheJson()
        {
            PublicKeyCredentialCreationOptions options = WebAuthnOptionsTestData.NewGenerator().CreateRegistrationOptions(
                WebAuthnOptionsTestData.NewUser(),
                WebAuthnUserVerificationRequirement.Required,
                excludeCredentialIds: Array.Empty<string>());

            Assert.IsNull(options.ExcludeCredentials);
        }

        [TestMethod]
        public void CreateRegistrationOptions_ExcludedCredentials_CarriesEachOneInOrder()
        {
            PublicKeyCredentialCreationOptions options = WebAuthnOptionsTestData.NewGenerator().CreateRegistrationOptions(
                WebAuthnOptionsTestData.NewUser(),
                WebAuthnUserVerificationRequirement.Required,
                excludeCredentialIds: new[] { WebAuthnOptionsTestData.FirstCredentialId, WebAuthnOptionsTestData.SecondCredentialId });

            CollectionAssert.AreEqual(
                new[] { WebAuthnOptionsTestData.FirstCredentialId, WebAuthnOptionsTestData.SecondCredentialId },
                options.ExcludeCredentials!.Select(descriptor => descriptor.Id).ToArray());
        }

        [TestMethod]
        public void CreateAuthenticationOptions_CarriesEachAllowedCredentialInOrder()
        {
            PublicKeyCredentialRequestOptions options = WebAuthnOptionsTestData.NewGenerator().CreateAuthenticationOptions(
                new[] { WebAuthnOptionsTestData.FirstCredentialId, WebAuthnOptionsTestData.SecondCredentialId },
                WebAuthnUserVerificationRequirement.Required);

            CollectionAssert.AreEqual(
                new[] { WebAuthnOptionsTestData.FirstCredentialId, WebAuthnOptionsTestData.SecondCredentialId },
                options.AllowCredentials.Select(descriptor => descriptor.Id).ToArray());
        }

        [TestMethod]
        public void CreateRegistrationOptions_NoAttachmentRequested_LeavesTheFieldOutOfTheJson()
        {
            PublicKeyCredentialCreationOptions options = WebAuthnOptionsTestData.NewGenerator()
                .CreateRegistrationOptions(WebAuthnOptionsTestData.NewUser(), WebAuthnUserVerificationRequirement.Required);

            Assert.IsNull(options.AuthenticatorSelection.AuthenticatorAttachment);
            Assert.IsFalse(options.ToJson().Contains("authenticatorAttachment", StringComparison.Ordinal));
        }

        [TestMethod]
        public void CreateRegistrationOptions_GivenTimeout_UsesItRatherThanTheDefault()
        {
            // The value is deliberately not 60000: asserting the default would pass with the caller's
            // argument dropped on the floor.
            PublicKeyCredentialCreationOptions options = WebAuthnOptionsTestData.NewGenerator().CreateRegistrationOptions(
                WebAuthnOptionsTestData.NewUser(),
                WebAuthnUserVerificationRequirement.Required,
                timeoutMilliseconds: 21000);

            Assert.AreEqual(21000, options.Timeout);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_GivenTimeout_UsesItRatherThanTheDefault()
        {
            PublicKeyCredentialRequestOptions options = WebAuthnOptionsTestData.NewGenerator().CreateAuthenticationOptions(
                new[] { WebAuthnOptionsTestData.FirstCredentialId },
                WebAuthnUserVerificationRequirement.Required,
                timeoutMilliseconds: 12345);

            Assert.AreEqual(12345, options.Timeout);
        }

        [TestMethod]
        public void CreateRegistrationOptions_NoTimeoutGiven_UsesTheDefault()
        {
            PublicKeyCredentialCreationOptions options = WebAuthnOptionsTestData.NewGenerator()
                .CreateRegistrationOptions(WebAuthnOptionsTestData.NewUser(), WebAuthnUserVerificationRequirement.Required);

            Assert.AreEqual(60000, options.Timeout);
        }
    }
}
