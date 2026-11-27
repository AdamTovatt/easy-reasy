using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Schemas;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The service provider's metadata, for identity provider administrators to configure this application from: its
    /// entity id, its assertion consumer service, and the public parts of its signing and decryption certificates.
    /// Serving it, at a URL or as a download, is the application's.
    /// </summary>
    public static class SamlServiceProviderMetadata
    {
        /// <summary>
        /// Builds the service provider's metadata XML.
        /// </summary>
        /// <param name="options">The same options the application registers with <c>AddEasyReasySamlAuth</c>.</param>
        /// <returns>One unsigned <c>md:EntityDescriptor</c> with an <c>SPSSODescriptor</c> that asks for signed assertions
        /// and a persistent NameID, names the HTTP-POST assertion consumer service, says whether requests are signed, and
        /// carries only certificates, never a private key.</returns>
        /// <exception cref="ArgumentException">The options are invalid. Every problem is reported in one exception.</exception>
        public static string Create(SamlAuthOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            IReadOnlyList<string> problems = options.FindProblems();
            if (problems.Count > 0)
            {
                throw new ArgumentException($"Invalid {nameof(SamlAuthOptions)}:\n{string.Join("\n", problems)}", nameof(options));
            }

            Saml2Configuration configuration = new Saml2Configuration { Issuer = options.ServiceProviderEntityId };
            EntityDescriptor descriptor = new EntityDescriptor(configuration, signMetadata: false)
            {
                SPSsoDescriptor = new SPSsoDescriptor
                {
                    AuthnRequestsSigned = options.SigningCertificate != null,
                    WantAssertionsSigned = true,
                    SigningCertificates = options.SigningCertificate == null ? null : [options.SigningCertificate],
                    EncryptionCertificates = options.DecryptionCertificates.Count == 0 ? null : options.DecryptionCertificates.ToList(),
                    NameIDFormats = [new Uri(SamlIdentitySource.PersistentNameIdFormat)],
                    AssertionConsumerServices =
                    [
                        new AssertionConsumerService { Binding = ProtocolBindings.HttpPost, Location = options.AssertionConsumerServiceUrl },
                    ],
                },
            };

            return descriptor.ToXmlDocument().OuterXml;
        }
    }
}
