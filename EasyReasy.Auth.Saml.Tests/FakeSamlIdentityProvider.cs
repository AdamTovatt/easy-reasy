using System.Security.Cryptography.X509Certificates;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// An in-process identity provider: an entity id and a signing key, answering an AuthnRequest with a response the
    /// validator accepts until a test changes it.
    /// </summary>
    internal sealed class FakeSamlIdentityProvider
    {
        public FakeSamlIdentityProvider(string entityId, X509Certificate2 signingCertificate)
        {
            EntityId = entityId;
            SigningCertificate = signingCertificate;
        }

        public string EntityId { get; }

        public X509Certificate2 SigningCertificate { get; }

        /// <summary>
        /// Gets the trust an app configures for this identity provider: its entity id and its certificate's public part.
        /// </summary>
        public SamlIdentityProviderTrust Trust => new SamlIdentityProviderTrust(EntityId, [SamlTestCertificates.PublicOnly(SigningCertificate)]);

        /// <summary>
        /// Answers the request <paramref name="requestId"/> from <paramref name="serviceProvider"/> for the user
        /// <paramref name="nameId"/>, valid from now on the real clock, as the library checks some times against it.
        /// </summary>
        public FakeSamlResponse Respond(string requestId, SamlServiceProviderSettings serviceProvider, string nameId = "user-1234")
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string assertionConsumerServiceUrl = serviceProvider.AssertionConsumerServiceUrl.AbsoluteUri;
            return new FakeSamlResponse
            {
                InResponseTo = requestId,
                ConfirmationInResponseTo = requestId,
                Destination = assertionConsumerServiceUrl,
                Recipient = assertionConsumerServiceUrl,
                ResponseIssuer = EntityId,
                AssertionIssuer = EntityId,
                AudienceRestrictions = [[serviceProvider.EntityId]],
                IssueInstant = now,
                ConditionsNotBefore = now.AddMinutes(-1),
                ConditionsNotOnOrAfter = now.AddMinutes(5),
                ConfirmationNotOnOrAfter = now.AddMinutes(5),
                NameId = nameId,
                SigningCertificate = SigningCertificate,
            };
        }
    }
}
