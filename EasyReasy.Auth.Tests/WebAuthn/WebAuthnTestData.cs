using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The relying party every WebAuthn test is written against, and the subjects built from it.
    /// </summary>
    internal static class WebAuthnTestData
    {
        /// <summary>The relying-party id the tests use.</summary>
        public const string RelyingPartyId = "example.com";

        /// <summary>An origin under <see cref="RelyingPartyId"/>.</summary>
        public const string Origin = "https://example.com";

        /// <summary>The human-readable relying-party name the tests use.</summary>
        public const string RelyingPartyName = "Contoso";

        /// <summary>
        /// The challenge a ceremony under test answers: 32 bytes, the length the generator issues and the
        /// length verification requires.
        /// </summary>
        public static readonly string Challenge = Base64UrlEncoding.Encode(CountingBytes(0, 1));

        /// <summary>
        /// A different challenge of the same length, for showing that a ceremony answering the wrong one is
        /// rejected. Every byte differs from <see cref="Challenge"/>, so a comparison that reads only part
        /// of the value cannot pass this by accident.
        /// </summary>
        public static readonly string OtherChallenge = Base64UrlEncoding.Encode(CountingBytes(255, -1));

        /// <summary>
        /// SHA-256 of <see cref="RelyingPartyId"/> — what an authenticator scoped to it reports. A property
        /// rather than a field so each caller gets its own array: a shared <c>static readonly byte[]</c> is
        /// writable by every test in the assembly. The two challenges above are strings, which are not, so
        /// they do not need the same treatment.
        /// </summary>
        public static byte[] RelyingPartyIdHash => SHA256.HashData(Encoding.UTF8.GetBytes(RelyingPartyId));

        /// <summary>The relying party the WebAuthn tests are written against.</summary>
        public static WebAuthnRelyingParty NewRelyingParty()
        {
            return new WebAuthnRelyingParty(RelyingPartyId, RelyingPartyName, new[] { Origin });
        }

        /// <summary>A verifier for that relying party.</summary>
        public static WebAuthnVerifier NewVerifier()
        {
            return new WebAuthnVerifier(NewRelyingParty());
        }

        /// <summary>An options generator for that relying party.</summary>
        public static WebAuthnOptionsGenerator NewGenerator()
        {
            return new WebAuthnOptionsGenerator(NewRelyingParty());
        }

        /// <summary>
        /// 32 bytes starting at <paramref name="first"/> and moving by <paramref name="step"/>, so two
        /// challenges can be built that differ in every byte.
        /// </summary>
        private static byte[] CountingBytes(int first, int step)
        {
            byte[] value = new byte[32];
            for (int index = 0; index < value.Length; index++)
            {
                value[index] = (byte)(first + (step * index));
            }

            return value;
        }
    }
}
