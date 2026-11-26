using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// Signs SAML elements the way an identity provider does, with System.Security.Cryptography.Xml directly, so the
    /// fake identity provider never relies on the library under test and can choose any algorithm.
    /// </summary>
    internal static class FakeXmlSigner
    {
        /// <summary>
        /// Adds an enveloped signature over <paramref name="element"/>, referencing its <c>ID</c>
        /// <paramref name="referenceCount"/> times, placed right after its <c>Issuer</c> child as the SAML schema requires.
        /// </summary>
        public static void Sign(
            XmlElement element,
            X509Certificate2 certificate,
            string signatureMethod,
            string digestMethod,
            string canonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl,
            int referenceCount = 1)
        {
            using RSA privateKey = certificate.GetRSAPrivateKey() ?? throw new ArgumentException("The certificate has no RSA private key.", nameof(certificate));

            SignedXml signedXml = new SignedXml(element)
            {
                SigningKey = privateKey,
            };
            signedXml.SignedInfo!.CanonicalizationMethod = canonicalizationMethod;
            signedXml.SignedInfo.SignatureMethod = signatureMethod;

            for (int index = 0; index < referenceCount; index++)
            {
                Reference reference = new Reference("#" + element.GetAttribute("ID"))
                {
                    DigestMethod = digestMethod,
                };
                reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
                reference.AddTransform(canonicalizationMethod == SignedXml.XmlDsigExcC14NWithCommentsTransformUrl
                    ? new XmlDsigExcC14NWithCommentsTransform()
                    : new XmlDsigExcC14NTransform());
                signedXml.AddReference(reference);
            }

            KeyInfo keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(certificate));
            signedXml.KeyInfo = keyInfo;

            signedXml.ComputeSignature();

            XmlElement signature = signedXml.GetXml();
            XmlElement issuer = element[FakeSamlResponse.IssuerElementName, FakeSamlResponse.AssertionNamespace]
                ?? throw new ArgumentException("The element has no Issuer to place the signature after.", nameof(element));
            element.InsertAfter(element.OwnerDocument.ImportNode(signature, true), issuer);
        }
    }
}
