using System.Security.Cryptography.X509Certificates;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// This application as a SAML service provider: its one entity id and assertion consumer service URL, the key that
    /// seals what the package keeps in <see cref="ISamlAuthStore"/>, how long a sign-in stays completable, and the keys it
    /// signs requests and decrypts assertions with. The identity providers are not configured here: the application's
    /// <see cref="ISamlIdentityProviderResolver"/> supplies them per request.
    /// </summary>
    public class SamlAuthOptions
    {
        /// <summary>
        /// The service provider's entity id, as the identity providers know it. Every assertion's audience restriction must
        /// name it.
        /// </summary>
        public required string ServiceProviderEntityId { get; init; }

        /// <summary>
        /// The absolute URL of the assertion consumer service: <see cref="SamlAuthPaths.AssertionConsumerService"/> as the
        /// browser reaches it, https (or http on a loopback or localhost host, as <see cref="SecureBrowserAddress"/>
        /// allows). Every response's <c>Destination</c> and bearer <c>Recipient</c> must equal it.
        /// </summary>
        public required Uri AssertionConsumerServiceUrl { get; init; }

        /// <summary>
        /// The 32-byte AES-256 key that seals the pending requests and completions the package stores, so the store holds
        /// no identity or rule in plaintext and a stored value cannot be forged. Give it its own key: never one another
        /// package or cipher uses. The key is copied when the options are registered.
        /// </summary>
        public required byte[] StateKey { get; init; }

        /// <summary>
        /// How long a begun sign-in stays completable at the assertion consumer service. Defaults to 10 minutes, which
        /// leaves room for a slow second factor at the identity provider. Its browser binding cookie lives this plus
        /// <see cref="CompletionLifetime"/>, so a response that arrives just in time can still be completed.
        /// </summary>
        public TimeSpan RequestLifetime { get; init; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// How long the outcome the assertion consumer service stores stays redeemable by its completion code. The
        /// completion page redeems it at once, so it defaults to 2 minutes.
        /// </summary>
        public TimeSpan CompletionLifetime { get; init; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// How often the package deletes expired pending requests and completions through
        /// <see cref="ISamlAuthStore.DeleteExpiredAsync"/>. Defaults to 1 hour; the first sweep runs one interval after
        /// start; at most <see cref="StoreSweep.MaxInterval"/>. An expired row is never accepted, so the interval only
        /// bounds how long one stays stored.
        /// </summary>
        public TimeSpan SweepInterval { get; init; } = TimeSpan.FromHours(1);

        /// <summary>
        /// A certificate with its RSA private key, which signs every AuthnRequest's query string, or null to send requests
        /// unsigned. Required by an identity provider whose metadata sets <c>WantAuthnRequestsSigned</c>.
        /// </summary>
        public X509Certificate2? SigningCertificate { get; init; }

        /// <summary>
        /// Certificates with their RSA private keys that decrypt assertions, several at once so the key can be rolled
        /// over. When any is given, a plaintext assertion is refused. Empty to accept plaintext assertions.
        /// </summary>
        public IReadOnlyList<X509Certificate2> DecryptionCertificates { get; init; } = [];

        /// <summary>
        /// Every problem with these options, reported by registration in one exception.
        /// </summary>
        /// <returns>One message per problem; empty when the options are valid.</returns>
        public IReadOnlyList<string> FindProblems()
        {
            List<string> problems = new List<string>();

            if (string.IsNullOrWhiteSpace(ServiceProviderEntityId))
            {
                problems.Add($"{nameof(ServiceProviderEntityId)} is required.");
            }

            if (!SecureBrowserAddress.IsAllowed(AssertionConsumerServiceUrl))
            {
                problems.Add($"{nameof(AssertionConsumerServiceUrl)} must be an absolute https URL (or http on a loopback or localhost host); got '{AssertionConsumerServiceUrl}'.");
            }

            if (StateKey == null || StateKey.Length != 32)
            {
                problems.Add($"{nameof(StateKey)} must be exactly 32 bytes for AES-256; got {StateKey?.Length.ToString() ?? "null"}.");
            }

            if (RequestLifetime <= TimeSpan.Zero)
            {
                problems.Add($"{nameof(RequestLifetime)} must be positive; got {RequestLifetime}.");
            }

            if (CompletionLifetime <= TimeSpan.Zero)
            {
                problems.Add($"{nameof(CompletionLifetime)} must be positive; got {CompletionLifetime}.");
            }

            if (SweepInterval <= TimeSpan.Zero || SweepInterval > StoreSweep.MaxInterval)
            {
                problems.Add($"{nameof(SweepInterval)} must be positive and at most {StoreSweep.MaxInterval.TotalDays} days, a timer's longest period; got {SweepInterval}.");
            }

            if (SigningCertificate != null && SigningCertificate.GetRSAPrivateKey() == null)
            {
                problems.Add($"{nameof(SigningCertificate)} must carry an RSA private key.");
            }

            if (DecryptionCertificates == null)
            {
                problems.Add($"{nameof(DecryptionCertificates)} must not be null; leave it empty to accept plaintext assertions.");
            }
            else if (DecryptionCertificates.Any(certificate => certificate == null || certificate.GetRSAPrivateKey() == null))
            {
                problems.Add($"Every one of {nameof(DecryptionCertificates)} must carry an RSA private key.");
            }

            return problems;
        }
    }
}
