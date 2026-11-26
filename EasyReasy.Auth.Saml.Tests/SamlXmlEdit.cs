using System.Security.Cryptography.Xml;
using System.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// Edits a built response the way an attacker who holds a valid one would.
    /// </summary>
    internal static class SamlXmlEdit
    {
        /// <summary>
        /// Returns the assertion directly inside the response.
        /// </summary>
        public static XmlElement Assertion(XmlDocument document)
        {
            return document.DocumentElement![FakeSamlResponse.AssertionElementName, FakeSamlResponse.AssertionNamespace]
                ?? throw new InvalidOperationException("The response has no plaintext assertion.");
        }

        /// <summary>
        /// Returns the signature directly inside <paramref name="element"/>.
        /// </summary>
        public static XmlElement Signature(XmlElement element)
        {
            return element["Signature", SignedXml.XmlDsigNamespaceUrl]
                ?? throw new InvalidOperationException("The element has no signature.");
        }

        /// <summary>
        /// Returns the NameID inside an assertion.
        /// </summary>
        public static XmlElement NameId(XmlElement assertion)
        {
            XmlElement subject = assertion["Subject", FakeSamlResponse.AssertionNamespace]
                ?? throw new InvalidOperationException("The assertion has no subject.");
            return subject["NameID", FakeSamlResponse.AssertionNamespace]
                ?? throw new InvalidOperationException("The subject has no NameID.");
        }

        /// <summary>
        /// Copies <paramref name="assertion"/> as an assertion about <paramref name="nameId"/>, with the given ID and
        /// without its signature unless <paramref name="keepSignature"/>. The copy is not yet in the document.
        /// </summary>
        public static XmlElement ForgeAssertion(XmlElement assertion, string nameId, string id, bool keepSignature)
        {
            XmlElement forged = (XmlElement)assertion.CloneNode(true);
            forged.SetAttribute("ID", id);
            NameId(forged).InnerText = nameId;
            if (!keepSignature)
            {
                forged.RemoveChild(Signature(forged));
            }

            return forged;
        }

        /// <summary>
        /// Appends <paramref name="content"/> to <paramref name="signature"/> inside a new <c>ds:Object</c>, which the
        /// enveloped-signature transform leaves out of the digest of the element the signature covers.
        /// </summary>
        public static void AppendToSignatureObject(XmlElement signature, XmlElement content)
        {
            XmlDocument document = signature.OwnerDocument;
            XmlElement signatureObject = document.CreateElement("ds", "Object", SignedXml.XmlDsigNamespaceUrl);
            signatureObject.AppendChild(content.OwnerDocument == document ? content : document.ImportNode(content, true));
            signature.AppendChild(signatureObject);
        }

        /// <summary>
        /// Returns the direct child of <paramref name="parent"/> in the assertion namespace named <paramref name="localName"/>.
        /// </summary>
        public static XmlElement AssertionChild(XmlElement parent, string localName)
        {
            return parent[localName, FakeSamlResponse.AssertionNamespace]
                ?? throw new InvalidOperationException($"The element has no {localName}.");
        }

        /// <summary>
        /// Adds an empty <c>samlp:Extensions</c> to the response, after its issuer and signature, and returns it.
        /// </summary>
        public static XmlElement AddExtensions(XmlDocument document)
        {
            XmlElement response = document.DocumentElement!;
            XmlElement extensions = document.CreateElement("samlp", "Extensions", FakeSamlResponse.ProtocolNamespace);
            XmlElement status = response["Status", FakeSamlResponse.ProtocolNamespace]
                ?? throw new InvalidOperationException("The response has no status.");
            response.InsertBefore(extensions, status);
            return extensions;
        }
    }
}
