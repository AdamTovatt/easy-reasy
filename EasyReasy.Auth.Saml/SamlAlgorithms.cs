using System.Security.Cryptography.Xml;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The XML signature and encryption algorithms a response may use. Anything else is refused: SHA-1 wherever it names
    /// the signature method or the digest of a reference, and any encryption that is not authenticated or whose key
    /// transport is not OAEP.
    /// </summary>
    internal static class SamlAlgorithms
    {
        /// <summary>
        /// XML Encryption 1.1's AES-128-GCM.
        /// </summary>
        public const string Aes128Gcm = "http://www.w3.org/2009/xmlenc11#aes128-gcm";

        /// <summary>
        /// XML Encryption 1.1's AES-256-GCM.
        /// </summary>
        public const string Aes256Gcm = "http://www.w3.org/2009/xmlenc11#aes256-gcm";

        /// <summary>
        /// XML Encryption 1.1's RSA-OAEP, which names its digest and mask generation function in the element. The
        /// specification defaults a missing one to SHA-1 and MGF1-SHA1, but ITfoxtec reads a missing mask generation
        /// function as MGF1-SHA256, so a key wrapped with the defaults fails to decrypt and the response is refused.
        /// </summary>
        public const string RsaOaep11 = "http://www.w3.org/2009/xmlenc11#rsa-oaep";

        /// <summary>
        /// The accepted data encryption methods for an <c>EncryptedData</c>: AES-GCM, which is authenticated, so an
        /// altered ciphertext fails as a whole instead of answering padding questions as AES-CBC does.
        /// </summary>
        public static readonly IReadOnlyList<string> DataEncryptionMethods =
        [
            Aes128Gcm,
            Aes256Gcm,
        ];

        /// <summary>
        /// The accepted key transport methods for an <c>EncryptedKey</c>: RSA-OAEP, never RSA PKCS#1 v1.5. Both
        /// identifiers are RSA-OAEP: XML Encryption 1.0's with MGF1-SHA1, and 1.1's, which names its digest and mask
        /// generation function in the element (Keycloak sends it).
        /// </summary>
        public static readonly IReadOnlyList<string> KeyTransportMethods =
        [
            EncryptedXml.XmlEncRSAOAEPUrl,
            RsaOaep11,
        ];

        /// <summary>
        /// The accepted signature methods: RSA PKCS#1 v1.5 with SHA-256, SHA-384 or SHA-512.
        /// </summary>
        public static readonly IReadOnlyList<string> SignatureMethods =
        [
            SignedXml.XmlDsigRSASHA256Url,
            SignedXml.XmlDsigRSASHA384Url,
            SignedXml.XmlDsigRSASHA512Url,
        ];

        /// <summary>
        /// The accepted digest methods for a signature's reference.
        /// </summary>
        public static readonly IReadOnlyList<string> DigestMethods =
        [
            SignedXml.XmlDsigSHA256Url,
            SignedXml.XmlDsigSHA384Url,
            SignedXml.XmlDsigSHA512Url,
        ];

        /// <summary>
        /// The accepted canonicalization methods, for the signed info and for a reference's transforms.
        /// </summary>
        public static readonly IReadOnlyList<string> CanonicalizationMethods =
        [
            SignedXml.XmlDsigExcC14NTransformUrl,
        ];
    }
}
