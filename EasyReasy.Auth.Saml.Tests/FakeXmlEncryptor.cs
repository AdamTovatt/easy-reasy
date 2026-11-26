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
        public const string Aes128Gcm = "http://www.w3.org/2009/xmlenc11#aes128-gcm";
        public const string Aes256Gcm = "http://www.w3.org/2009/xmlenc11#aes256-gcm";

        private const int GcmNonceSize = 12;
        private const int GcmTagSize = 16;

        /// <summary>
        /// Replaces <paramref name="assertion"/> with an <c>EncryptedAssertion</c> holding it encrypted with
        /// <paramref name="dataEncryptionMethod"/>, under a key wrapped for <paramref name="certificate"/> with
        /// <paramref name="keyTransportMethod"/>.
        /// </summary>
        public static void EncryptAssertion(XmlElement assertion, X509Certificate2 certificate, string dataEncryptionMethod, string keyTransportMethod)
        {
            XmlElement encryptedData = Encrypt(assertion, certificate, dataEncryptionMethod, keyTransportMethod);
            XmlDocument document = assertion.OwnerDocument;
            XmlElement wrapper = document.CreateElement("saml", "EncryptedAssertion", FakeSamlResponse.AssertionNamespace);
            wrapper.AppendChild(document.ImportNode(encryptedData, true));
            assertion.ParentNode!.ReplaceChild(wrapper, assertion);
        }

        /// <summary>
        /// Encrypts <paramref name="element"/> and returns the <c>EncryptedData</c>, which is not yet in any document.
        /// </summary>
        public static XmlElement Encrypt(XmlElement element, X509Certificate2 certificate, string dataEncryptionMethod, string keyTransportMethod)
        {
            byte[] plaintext = Encoding.UTF8.GetBytes(element.OuterXml);
            byte[] key = RandomNumberGenerator.GetBytes(KeySizeOf(dataEncryptionMethod));
            byte[] cipherValue = IsGcm(dataEncryptionMethod) ? EncryptGcm(plaintext, key) : EncryptCbc(element, key);

            using RSA publicKey = certificate.GetRSAPublicKey() ?? throw new ArgumentException("The certificate has no RSA public key.", nameof(certificate));
            bool useOaep = keyTransportMethod switch
            {
                EncryptedXml.XmlEncRSAOAEPUrl => true,
                EncryptedXml.XmlEncRSA15Url => false,
                _ => throw new ArgumentException($"Unsupported key transport method {keyTransportMethod}.", nameof(keyTransportMethod)),
            };
            EncryptedKey encryptedKey = new EncryptedKey
            {
                EncryptionMethod = new EncryptionMethod(keyTransportMethod),
                CipherData = new CipherData(EncryptedXml.EncryptKey(key, publicKey, useOaep)),
            };

            EncryptedData encryptedData = new EncryptedData
            {
                Type = EncryptedXml.XmlEncElementUrl,
                EncryptionMethod = new EncryptionMethod(dataEncryptionMethod),
                KeyInfo = new KeyInfo(),
            };
            encryptedData.KeyInfo.AddClause(new KeyInfoEncryptedKey(encryptedKey));
            encryptedData.CipherData.CipherValue = cipherValue;
            return encryptedData.GetXml();
        }

        private static bool IsGcm(string dataEncryptionMethod)
        {
            return dataEncryptionMethod is Aes128Gcm or Aes256Gcm;
        }

        private static int KeySizeOf(string dataEncryptionMethod)
        {
            return dataEncryptionMethod switch
            {
                Aes128Gcm or EncryptedXml.XmlEncAES128Url => 16,
                Aes256Gcm or EncryptedXml.XmlEncAES256Url => 32,
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
