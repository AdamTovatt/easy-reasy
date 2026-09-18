namespace EasyReasy.Auth
{
    /// <summary>
    /// The COSE (RFC 8152) signature algorithms this library supports for WebAuthn credentials.
    /// The numeric values are the IANA COSE Algorithm identifiers that appear on the wire, both in
    /// <c>pubKeyCredParams</c> and inside the credential's COSE public key.
    /// </summary>
    /// <remarks>
    /// Deliberately a closed set of two. ES256 is the algorithm the WebAuthn ecosystem converged on:
    /// platform authenticators such as Touch ID and Face ID produce it, FIDO2 security keys implement it,
    /// and it is what a relying party offers first. RS256 is the fallback that keeps an authenticator
    /// implementing only it enrollable rather than locked out. Both map onto built-in .NET primitives, so
    /// supporting them adds no verification code beyond selecting the key type. An algorithm outside this
    /// set is rejected by name rather than silently accepted.
    /// </remarks>
    public enum CoseAlgorithm
    {
        /// <summary>ECDSA over NIST P-256 with SHA-256 (COSE identifier <c>-7</c>).</summary>
        Es256 = -7,

        /// <summary>RSASSA-PKCS1-v1_5 with SHA-256 (COSE identifier <c>-257</c>).</summary>
        Rs256 = -257,
    }
}
