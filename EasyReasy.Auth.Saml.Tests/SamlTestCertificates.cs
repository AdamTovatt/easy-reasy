using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// Self-signed RSA certificates for the fake identity providers and the service provider, made once per test run
    /// because generating RSA keys is slow.
    /// </summary>
    internal static class SamlTestCertificates
    {
        private static readonly Lazy<X509Certificate2> _identityProvider = new Lazy<X509Certificate2>(() => Create("CN=Fake IdP"));
        private static readonly Lazy<X509Certificate2> _identityProviderRollover = new Lazy<X509Certificate2>(() => Create("CN=Fake IdP rollover"));
        private static readonly Lazy<X509Certificate2> _otherIdentityProvider = new Lazy<X509Certificate2>(() => Create("CN=Other IdP"));
        private static readonly Lazy<X509Certificate2> _attacker = new Lazy<X509Certificate2>(() => Create("CN=Attacker"));
        private static readonly Lazy<X509Certificate2> _serviceProviderDecryption = new Lazy<X509Certificate2>(() => Create("CN=Fake SP"));

        /// <summary>
        /// Gets the expected identity provider's signing certificate, with its private key.
        /// </summary>
        public static X509Certificate2 IdentityProvider => _identityProvider.Value;

        /// <summary>
        /// Gets the expected identity provider's next signing certificate, with its private key.
        /// </summary>
        public static X509Certificate2 IdentityProviderRollover => _identityProviderRollover.Value;

        /// <summary>
        /// Gets another identity provider's signing certificate, with its private key.
        /// </summary>
        public static X509Certificate2 OtherIdentityProvider => _otherIdentityProvider.Value;

        /// <summary>
        /// Gets a certificate no service provider pins, with its private key.
        /// </summary>
        public static X509Certificate2 Attacker => _attacker.Value;

        /// <summary>
        /// Gets the service provider's decryption certificate, with its private key.
        /// </summary>
        public static X509Certificate2 ServiceProviderDecryption => _serviceProviderDecryption.Value;

        /// <summary>
        /// Returns the certificate without its private key, as an app pins it from the identity provider's metadata.
        /// </summary>
        public static X509Certificate2 PublicOnly(X509Certificate2 certificate)
        {
            return X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
        }

        private static X509Certificate2 Create(string subject)
        {
            using RSA key = RSA.Create(2048);
            CertificateRequest request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return request.CreateSelfSigned(now.AddDays(-1), now.AddYears(1));
        }
    }
}
