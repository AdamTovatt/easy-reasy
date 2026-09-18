namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The published TOTP test vectors both <see cref="Rfc6238TotpGeneratorTests"/> and
    /// <see cref="Rfc6238TotpProvisioningTests"/> assert against, kept in one place so the two files
    /// cannot drift into testing different secrets.
    /// </summary>
    internal static class Rfc6238TestVectors
    {
        /// <summary>
        /// RFC 6238 Appendix B's ASCII seed for the SHA-1 vectors. A span rather than a
        /// <see cref="byte"/> array: a shared <c>static readonly</c> array is a readonly reference to
        /// writable contents, which either test class could corrupt for the other.
        /// </summary>
        internal static ReadOnlySpan<byte> Seed => "12345678901234567890"u8;

        /// <summary>The base32 form of <see cref="Seed"/> — the well-known string authenticator apps show for it.</summary>
        internal const string SeedBase32 = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
    }
}
