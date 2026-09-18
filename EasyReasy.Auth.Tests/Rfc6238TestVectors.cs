using System.Text;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The published TOTP test vectors both <see cref="Rfc6238TotpGeneratorTests"/> and
    /// <see cref="Rfc6238TotpProvisioningTests"/> assert against, kept in one place so the two files
    /// cannot drift into testing different secrets.
    /// </summary>
    internal static class Rfc6238TestVectors
    {
        /// <summary>RFC 6238 Appendix B's ASCII seed for the SHA-1 vectors.</summary>
        internal static readonly byte[] Seed = Encoding.ASCII.GetBytes("12345678901234567890");

        /// <summary>The base32 form of <see cref="Seed"/> — the well-known string authenticator apps show for it.</summary>
        internal const string SeedBase32 = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
    }
}
