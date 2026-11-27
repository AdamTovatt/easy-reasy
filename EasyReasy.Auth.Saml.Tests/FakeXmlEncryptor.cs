using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// Encrypts an assertion the way an identity provider does, with any data encryption and key transport method, so
    /// the tests can send what the service provider must refuse as well as what it accepts.
    /// </summary>
    internal static class FakeXmlEncryptor
    {
        /// <summary>
        /// XML Encryption 1.1's mask generation function MGF1 with SHA-1.
        /// </summary>
        public const string Mgf1Sha1 = "http://www.w3.org/2009/xmlenc11#mgf1sha1";

        /// <summary>
        /// XML Encryption 1.1's mask generation function MGF1 with SHA-256, which Keycloak sends with a SHA-256 digest.
        /// </summary>
        public const string Mgf1Sha256 = "http://www.w3.org/2009/xmlenc11#mgf1sha256";

        /// <summary>
        /// XML Encryption 1.1's mask generation function MGF1 with SHA-512.
        /// </summary>
        public const string Mgf1Sha512 = "http://www.w3.org/2009/xmlenc11#mgf1sha512";

        private const string XmlEnc11Namespace = "http://www.w3.org/2009/xmlenc11#";

        private const int GcmNonceSize = 12;
        private const int GcmTagSize = 16;

        /// <summary>
        /// Replaces <paramref name="assertion"/> with an <c>EncryptedAssertion</c> holding it encrypted with
        /// <paramref name="dataEncryptionMethod"/>, under a key wrapped for <paramref name="certificate"/> with
        /// <paramref name="keyTransportMethod"/>. For <see cref="SamlAlgorithms.RsaOaep11"/>, the key is wrapped with OAEP
        /// over <paramref name="oaepDigestMethod"/> (SHA-1 when null), and the element names that digest and
        /// <paramref name="oaepMaskGenerationFunction"/>, each only when given, whether or not they match.
        /// </summary>
        public static void EncryptAssertion(
            XmlElement assertion,
            X509Certificate2 certificate,
            string dataEncryptionMethod,
            string keyTransportMethod,
            string? oaepDigestMethod = null,
            string? oaepMaskGenerationFunction = null)
        {
            XmlElement encryptedData = Encrypt(assertion, certificate, dataEncryptionMethod, keyTransportMethod, oaepDigestMethod, oaepMaskGenerationFunction);
            XmlDocument document = assertion.OwnerDocument;
            XmlElement wrapper = document.CreateElement("saml", "EncryptedAssertion", FakeSamlResponse.AssertionNamespace);
            wrapper.AppendChild(document.ImportNode(encryptedData, true));
            assertion.ParentNode!.ReplaceChild(wrapper, assertion);
        }

        /// <summary>
        /// Encrypts <paramref name="element"/> and returns the <c>EncryptedData</c>, which is not yet in any document. The
        /// OAEP parameters are as for <see cref="EncryptAssertion"/>.
        /// </summary>
        public static XmlElement Encrypt(
            XmlElement element,
            X509Certificate2 certificate,
            string dataEncryptionMethod,
            string keyTransportMethod,
            string? oaepDigestMethod = null,
            string? oaepMaskGenerationFunction = null)
        {
            byte[] plaintext = Encoding.UTF8.GetBytes(element.OuterXml);
            byte[] key = RandomNumberGenerator.GetBytes(KeySizeOf(dataEncryptionMethod));
            byte[] cipherValue = IsGcm(dataEncryptionMethod) ? EncryptGcm(plaintext, key) : EncryptCbc(element, key);

            using RSA publicKey = certificate.GetRSAPublicKey() ?? throw new ArgumentException("The certificate has no RSA public key.", nameof(certificate));
            byte[] wrappedKey = keyTransportMethod switch
            {
                EncryptedXml.XmlEncRSAOAEPUrl => EncryptedXml.EncryptKey(key, publicKey, true),
                EncryptedXml.XmlEncRSA15Url => EncryptedXml.EncryptKey(key, publicKey, false),
                SamlAlgorithms.RsaOaep11 => publicKey.Encrypt(key, Oaep11Padding(oaepDigestMethod)),
                _ => throw new ArgumentException($"Unsupported key transport method {keyTransportMethod}.", nameof(keyTransportMethod)),
            };
            EncryptedKey encryptedKey = new EncryptedKey
            {
                EncryptionMethod = new EncryptionMethod(keyTransportMethod),
                CipherData = new CipherData(wrappedKey),
            };

            EncryptedData encryptedData = new EncryptedData
            {
                Type = EncryptedXml.XmlEncElementUrl,
                EncryptionMethod = new EncryptionMethod(dataEncryptionMethod),
                KeyInfo = new KeyInfo(),
            };
            encryptedData.KeyInfo.AddClause(new KeyInfoEncryptedKey(encryptedKey));
            encryptedData.CipherData.CipherValue = cipherValue;
            XmlElement encryptedDataXml = encryptedData.GetXml();
            if (keyTransportMethod == SamlAlgorithms.RsaOaep11)
            {
                AddOaep11Parameters(encryptedDataXml, oaepDigestMethod, oaepMaskGenerationFunction);
            }

            return encryptedDataXml;
        }

        private static RSAEncryptionPadding Oaep11Padding(string? digestMethod)
        {
            return digestMethod switch
            {
                null or SignedXml.XmlDsigSHA1Url => RSAEncryptionPadding.OaepSHA1,
                SignedXml.XmlDsigSHA256Url => RSAEncryptionPadding.OaepSHA256,
                _ => throw new ArgumentException($"Unsupported OAEP digest method {digestMethod}.", nameof(digestMethod)),
            };
        }

        private static void AddOaep11Parameters(XmlElement encryptedDataXml, string? digestMethod, string? maskGenerationFunction)
        {
            XmlNamespaceManager namespaces = new XmlNamespaceManager(encryptedDataXml.OwnerDocument.NameTable);
            namespaces.AddNamespace("enc", EncryptedXml.XmlEncNamespaceUrl);
            XmlElement keyMethod = (XmlElement)encryptedDataXml.SelectSingleNode("descendant::enc:EncryptedKey/enc:EncryptionMethod", namespaces)!;
            if (digestMethod != null)
            {
                XmlElement digest = encryptedDataXml.OwnerDocument.CreateElement("ds", "DigestMethod", SignedXml.XmlDsigNamespaceUrl);
                digest.SetAttribute("Algorithm", digestMethod);
                keyMethod.AppendChild(digest);
            }

            if (maskGenerationFunction != null)
            {
                XmlElement mgf = encryptedDataXml.OwnerDocument.CreateElement("xenc11", "MGF", XmlEnc11Namespace);
                mgf.SetAttribute("Algorithm", maskGenerationFunction);
                keyMethod.AppendChild(mgf);
            }
        }

        private static bool IsGcm(string dataEncryptionMethod)
        {
            return dataEncryptionMethod is SamlAlgorithms.Aes128Gcm or SamlAlgorithms.Aes256Gcm;
        }

        private static int KeySizeOf(string dataEncryptionMethod)
        {
            return dataEncryptionMethod switch
            {
                SamlAlgorithms.Aes128Gcm or EncryptedXml.XmlEncAES128Url => 16,
                SamlAlgorithms.Aes256Gcm or EncryptedXml.XmlEncAES256Url => 32,
                _ => throw new ArgumentException($"Unsupported data encryption method {dataEncryptionMethod}.", nameof(dataEncryptionMethod)),
            };
        }

        private static byte[] EncryptGcm(byte[] plaintext, byte[] key)
        {
            // XML Encryption 1.1 lays AES-GCM out as the 96-bit nonce, the ciphertext, then the 128-bit tag.
            byte[] nonce = RandomNumberGenerator.GetBytes(GcmNonceSize);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[GcmTagSize];
            using AesGcm aesGcm = new AesGcm(key, GcmTagSize);
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
            return [.. nonce, .. ciphertext, .. tag];
        }

        private static byte[] EncryptCbc(XmlElement element, byte[] key)
        {
            using Aes aes = Aes.Create();
            aes.Key = key;
            return new EncryptedXml().EncryptData(element, aes, false);
        }
    }
}
