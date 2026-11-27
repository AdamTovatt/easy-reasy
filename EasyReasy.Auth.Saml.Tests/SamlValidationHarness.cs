using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// One sign-in in progress: a service provider that sent an AuthnRequest to the fake identity provider, and the
    /// validator that checks what comes back.
    /// </summary>
    internal sealed class SamlValidationHarness
    {
        public const string ServiceProviderEntityId = "https://app.example.test/saml";
        public const string AssertionConsumerServiceUrl = "https://app.example.test/auth/saml/acs";

        public SamlValidationHarness(IReadOnlyList<X509Certificate2>? decryptionCertificates = null, TimeProvider? timeProvider = null)
        {
            ServiceProvider = CreateOptions(decryptionCertificates ?? []);
            Validator = new SamlResponseValidator(
                ServiceProvider.ServiceProviderEntityId,
                ServiceProvider.AssertionConsumerServiceUrl,
                ServiceProvider.DecryptionCertificates,
                timeProvider ?? TimeProvider.System);
        }

        public SamlAuthOptions ServiceProvider { get; }

        public SamlResponseValidator Validator { get; }

        public FakeSamlIdentityProvider IdentityProvider { get; } = new FakeSamlIdentityProvider("https://idp.example.test/metadata", SamlTestCertificates.IdentityProvider);

        public FakeSamlIdentityProvider OtherIdentityProvider { get; } = new FakeSamlIdentityProvider("https://other-idp.example.test/metadata", SamlTestCertificates.OtherIdentityProvider);

        /// <summary>
        /// Gets the ID of the AuthnRequest this sign-in sent.
        /// </summary>
        public string RequestId { get; } = "_request-" + Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets the expected identity provider's answer to this sign-in's request.
        /// </summary>
        public FakeSamlResponse Respond(string nameId = "user-1234")
        {
            return IdentityProvider.Respond(RequestId, ServiceProvider, nameId);
        }

        /// <summary>
        /// Validates a response to this sign-in's request as from the expected identity provider.
        /// </summary>
        public SamlValidatedAssertion Validate(string samlResponse)
        {
            return Validator.Validate(samlResponse, RequestId, IdentityProvider.Provider);
        }

        /// <summary>
        /// The service provider's options the tests share: this harness's entity id and assertion consumer service URL, a
        /// fresh state key, and the given decryption certificates.
        /// </summary>
        public static SamlAuthOptions CreateOptions(IReadOnlyList<X509Certificate2> decryptionCertificates, X509Certificate2? signingCertificate = null, TimeSpan? sweepInterval = null)
        {
            return new SamlAuthOptions
            {
                SweepInterval = sweepInterval ?? TimeSpan.FromHours(1),
                ServiceProviderEntityId = ServiceProviderEntityId,
                AssertionConsumerServiceUrl = new Uri(AssertionConsumerServiceUrl),
                StateKey = RandomNumberGenerator.GetBytes(32),
                DecryptionCertificates = decryptionCertificates,
                SigningCertificate = signingCertificate,
            };
        }

        /// <summary>
        /// Validates a response that must be refused, and returns the refusal.
        /// </summary>
        public SamlResponseRejectedException Reject(string samlResponse)
        {
            return Assert.ThrowsException<SamlResponseRejectedException>(() => Validate(samlResponse));
        }
    }
}
