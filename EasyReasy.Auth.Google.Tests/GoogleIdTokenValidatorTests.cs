using EasyReasy.Auth.Google;
using Google.Apis.Auth;

namespace EasyReasy.Auth.Google.Tests
{
    /// <summary>
    /// Unit tests for the post-validation policy in <see cref="GoogleIdTokenValidator.ValidatePolicyAndBuildUserInfo"/> —
    /// hosted-domain allowlisting, email-verification enforcement, and payload mapping. The network-bound
    /// token validation itself is not exercised here.
    /// </summary>
    [TestClass]
    public class GoogleIdTokenValidatorTests
    {
        private static GoogleJsonWebSignature.Payload CreatePayload(
            string subject = "sub-1",
            string? email = "user@example.com",
            bool emailVerified = true,
            string? hostedDomain = null,
            string? name = "Test User",
            string? picture = "https://example.com/p.jpg")
        {
            return new GoogleJsonWebSignature.Payload
            {
                Subject = subject,
                Email = email,
                EmailVerified = emailVerified,
                HostedDomain = hostedDomain,
                Name = name,
                Picture = picture,
            };
        }

        [TestMethod]
        public void ValidatePolicyAndBuildUserInfo_MapsAllPayloadFields()
        {
            GoogleAuthOptions options = new GoogleAuthOptions { ClientId = "client-id" };
            GoogleJsonWebSignature.Payload payload = CreatePayload(
                subject: "sub-42",
                email: "person@example.com",
                emailVerified: true,
                name: "Person",
                picture: "https://example.com/x.jpg");

            GoogleUserInfo userInfo = GoogleIdTokenValidator.ValidatePolicyAndBuildUserInfo(payload, options);

            Assert.AreEqual("sub-42", userInfo.Subject);
            Assert.AreEqual("person@example.com", userInfo.Email);
            Assert.IsTrue(userInfo.EmailVerified);
            Assert.AreEqual("Person", userInfo.Name);
            Assert.AreEqual("https://example.com/x.jpg", userInfo.PictureUrl);
        }

        [TestMethod]
        public void ValidatePolicyAndBuildUserInfo_NoAllowedDomains_AcceptsAnyDomain()
        {
            GoogleAuthOptions options = new GoogleAuthOptions { ClientId = "client-id" };
            GoogleJsonWebSignature.Payload payload = CreatePayload(hostedDomain: "anything.com");

            GoogleUserInfo userInfo = GoogleIdTokenValidator.ValidatePolicyAndBuildUserInfo(payload, options);

            Assert.AreEqual("user@example.com", userInfo.Email);
        }

        [TestMethod]
        public void ValidatePolicyAndBuildUserInfo_AllowedDomainExactMatch_Accepts()
        {
            GoogleAuthOptions options = new GoogleAuthOptions { ClientId = "client-id", AllowedHostedDomains = new[] { "example.com" } };
            GoogleJsonWebSignature.Payload payload = CreatePayload(hostedDomain: "example.com");

            GoogleUserInfo userInfo = GoogleIdTokenValidator.ValidatePolicyAndBuildUserInfo(payload, options);

            Assert.AreEqual("user@example.com", userInfo.Email);
        }

        [TestMethod]
        public void ValidatePolicyAndBuildUserInfo_AllowedDomainDifferentCase_Accepts()
        {
            GoogleAuthOptions options = new GoogleAuthOptions { ClientId = "client-id", AllowedHostedDomains = new[] { "Example.COM" } };
            GoogleJsonWebSignature.Payload payload = CreatePayload(hostedDomain: "example.com");

            GoogleUserInfo userInfo = GoogleIdTokenValidator.ValidatePolicyAndBuildUserInfo(payload, options);

            Assert.AreEqual("user@example.com", userInfo.Email);
        }

        [TestMethod]
        public void ValidatePolicyAndBuildUserInfo_DisallowedDomain_Throws()
        {
            GoogleAuthOptions options = new GoogleAuthOptions { ClientId = "client-id", AllowedHostedDomains = new[] { "example.com" } };
            GoogleJsonWebSignature.Payload payload = CreatePayload(hostedDomain: "evil.com");

            Assert.ThrowsException<GoogleTokenValidationException>(() => GoogleIdTokenValidator.ValidatePolicyAndBuildUserInfo(payload, options));
        }

        [TestMethod]
        public void ValidatePolicyAndBuildUserInfo_MissingHostedDomainWhenRestricted_Throws()
        {
            GoogleAuthOptions options = new GoogleAuthOptions { ClientId = "client-id", AllowedHostedDomains = new[] { "example.com" } };
            GoogleJsonWebSignature.Payload payload = CreatePayload(hostedDomain: null);

            Assert.ThrowsException<GoogleTokenValidationException>(() => GoogleIdTokenValidator.ValidatePolicyAndBuildUserInfo(payload, options));
        }

        [TestMethod]
        public void ValidatePolicyAndBuildUserInfo_UnverifiedEmailWhenRequired_Throws()
        {
            GoogleAuthOptions options = new GoogleAuthOptions { ClientId = "client-id" }; // RequireVerifiedEmail defaults to true
            GoogleJsonWebSignature.Payload payload = CreatePayload(emailVerified: false);

            Assert.ThrowsException<GoogleTokenValidationException>(() => GoogleIdTokenValidator.ValidatePolicyAndBuildUserInfo(payload, options));
        }

        [TestMethod]
        public void ValidatePolicyAndBuildUserInfo_UnverifiedEmailWhenNotRequired_Accepts()
        {
            GoogleAuthOptions options = new GoogleAuthOptions { ClientId = "client-id", RequireVerifiedEmail = false };
            GoogleJsonWebSignature.Payload payload = CreatePayload(emailVerified: false);

            GoogleUserInfo userInfo = GoogleIdTokenValidator.ValidatePolicyAndBuildUserInfo(payload, options);

            Assert.IsFalse(userInfo.EmailVerified);
        }

        [TestMethod]
        public void ValidatePolicyAndBuildUserInfo_EmptyAllowedDomains_AcceptsAnyDomain()
        {
            GoogleAuthOptions options = new GoogleAuthOptions { ClientId = "client-id", AllowedHostedDomains = new string[0] };
            GoogleJsonWebSignature.Payload payload = CreatePayload(hostedDomain: "anything.com");

            GoogleUserInfo userInfo = GoogleIdTokenValidator.ValidatePolicyAndBuildUserInfo(payload, options);

            Assert.AreEqual("user@example.com", userInfo.Email);
        }

        [TestMethod]
        public void ValidatePolicyAndBuildUserInfo_MissingEmail_Throws()
        {
            GoogleAuthOptions options = new GoogleAuthOptions { ClientId = "client-id", RequireVerifiedEmail = false };
            GoogleJsonWebSignature.Payload payload = CreatePayload(email: null, emailVerified: false);

            Assert.ThrowsException<GoogleTokenValidationException>(() => GoogleIdTokenValidator.ValidatePolicyAndBuildUserInfo(payload, options));
        }
    }
}
