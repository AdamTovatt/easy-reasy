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
            ServiceProvider = new SamlServiceProviderSettings(ServiceProviderEntityId, new Uri(AssertionConsumerServiceUrl), decryptionCertificates ?? []);
            Validator = new SamlResponseValidator(ServiceProvider, timeProvider ?? TimeProvider.System);
        }

        public SamlServiceProviderSettings ServiceProvider { get; }

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
            return Validator.Validate(samlResponse, RequestId, IdentityProvider.Trust);
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
