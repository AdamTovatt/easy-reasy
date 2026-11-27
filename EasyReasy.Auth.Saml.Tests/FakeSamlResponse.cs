using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// A SAML response the fake identity provider sends, built field by field so a test can make any part of it wrong.
    /// The defaults, set by <see cref="FakeSamlIdentityProvider.Respond"/>, are a response the validator accepts.
    /// </summary>
    internal sealed class FakeSamlResponse
    {
        public const string ProtocolNamespace = "urn:oasis:names:tc:SAML:2.0:protocol";
        public const string AssertionNamespace = "urn:oasis:names:tc:SAML:2.0:assertion";
        public const string IssuerElementName = "Issuer";
        public const string AssertionElementName = "Assertion";
        public const string SuccessStatus = "urn:oasis:names:tc:SAML:2.0:status:Success";
        public const string PersistentNameIdFormat = "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent";
        public const string BearerMethod = "urn:oasis:names:tc:SAML:2.0:cm:bearer";

        public string ResponseId { get; set; } = "_response-" + Guid.NewGuid().ToString("N");
        public string AssertionId { get; set; } = "_assertion-" + Guid.NewGuid().ToString("N");
        public string StatusCode { get; set; } = SuccessStatus;

        /// <summary>The response's <c>InResponseTo</c>; omitted when <see langword="null"/>.</summary>
        public required string? InResponseTo { get; set; }

        /// <summary>The bearer confirmation's <c>InResponseTo</c>; omitted when <see langword="null"/>.</summary>
        public required string? ConfirmationInResponseTo { get; set; }

        /// <summary>The response's <c>Destination</c>; omitted when <see langword="null"/>.</summary>
        public required string? Destination { get; set; }

        /// <summary>The bearer confirmation's <c>Recipient</c>; omitted when <see langword="null"/>.</summary>
        public required string? Recipient { get; set; }

        /// <summary>The response's <c>Issuer</c>; omitted when <see langword="null"/>.</summary>
        public required string? ResponseIssuer { get; set; }

        /// <summary>The assertion's <c>Issuer</c>; the signature is placed after it, so it is always present.</summary>
        public required string AssertionIssuer { get; set; }

        /// <summary>One <c>AudienceRestriction</c> per inner list.</summary>
        public required List<List<string>> AudienceRestrictions { get; set; }

        public required DateTimeOffset IssueInstant { get; set; }
        public required DateTimeOffset? ConditionsNotBefore { get; set; }
        public required DateTimeOffset? ConditionsNotOnOrAfter { get; set; }
        public DateTimeOffset? ConfirmationNotBefore { get; set; }
        public required DateTimeOffset? ConfirmationNotOnOrAfter { get; set; }

        public required string NameId { get; set; }
        public string NameIdFormat { get; set; } = PersistentNameIdFormat;
        public string? NameQualifier { get; set; }
        public string? ServiceProviderNameQualifier { get; set; }

        /// <summary>The AuthnStatement's <c>AuthnInstant</c>; <see cref="IssueInstant"/> when null.</summary>
        public DateTimeOffset? AuthenticationInstant { get; set; }

        /// <summary>The AuthnStatement's <c>AuthnContextClassRef</c>; omitted when null.</summary>
        public string? AuthnContextClassRef { get; set; } = "urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport";

        /// <summary>The AuthnStatement's <c>AuthnContextDeclRef</c>; omitted when null.</summary>
        public string? AuthnContextDeclRef { get; set; }

        /// <summary>The attributes, each with its values; no AttributeStatement when empty.</summary>
        public Dictionary<string, List<string>> Attributes { get; set; } = new Dictionary<string, List<string>>();

        public bool SignAssertion { get; set; } = true;
        public bool SignResponse { get; set; }
        public required X509Certificate2 SigningCertificate { get; set; }
        public string SignatureMethod { get; set; } = SignedXml.XmlDsigRSASHA256Url;
        public string DigestMethod { get; set; } = SignedXml.XmlDsigSHA256Url;
        public string CanonicalizationMethod { get; set; } = SignedXml.XmlDsigExcC14NTransformUrl;
        public int SignatureReferenceCount { get; set; } = 1;

        /// <summary>When set, the signed assertion is encrypted for this certificate.</summary>
        public X509Certificate2? EncryptFor { get; set; }
        public string DataEncryptionMethod { get; set; } = FakeXmlEncryptor.Aes256Gcm;
        public string KeyTransportMethod { get; set; } = EncryptedXml.XmlEncRSAOAEPUrl;

        /// <summary>
        /// Changes the assertion after it is built and before it is signed, as an identity provider that sends it that
        /// way would, so the signature still covers it.
        /// </summary>
        public Action<XmlElement>? EditAssertionBeforeSigning { get; set; }

        /// <summary>
        /// Encodes a response document as the base64 <c>SAMLResponse</c> form value.
        /// </summary>
        public static string Encode(XmlDocument document)
        {
            return Encode(document.OuterXml);
        }

        /// <summary>
        /// Encodes response XML as the base64 <c>SAMLResponse</c> form value.
        /// </summary>
        public static string Encode(string xml)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));
        }

        /// <summary>
        /// Builds the response: the assertion is signed, then encrypted, then the response is signed, each when asked.
        /// </summary>
        public XmlDocument BuildDocument()
        {
            XmlDocument document = new XmlDocument { PreserveWhitespace = true };
            XmlElement response = document.CreateElement("samlp", "Response", ProtocolNamespace);
            document.AppendChild(response);
            response.SetAttribute("xmlns:saml", AssertionNamespace);
            response.SetAttribute("ID", ResponseId);
            response.SetAttribute("Version", "2.0");
            response.SetAttribute("IssueInstant", FormatTime(IssueInstant));
            SetOptionalAttribute(response, "Destination", Destination);
            SetOptionalAttribute(response, "InResponseTo", InResponseTo);

            if (ResponseIssuer != null)
            {
                AppendAssertionElement(response, IssuerElementName, ResponseIssuer);
            }

            XmlElement status = AppendElement(response, "samlp", "Status", ProtocolNamespace);
            AppendElement(status, "samlp", "StatusCode", ProtocolNamespace).SetAttribute("Value", StatusCode);

            XmlElement assertion = AppendAssertion(response);
            EditAssertionBeforeSigning?.Invoke(assertion);

            if (SignAssertion)
            {
                FakeXmlSigner.Sign(assertion, SigningCertificate, SignatureMethod, DigestMethod, CanonicalizationMethod, SignatureReferenceCount);
            }

            if (EncryptFor != null)
            {
                FakeXmlEncryptor.EncryptAssertion(assertion, EncryptFor, DataEncryptionMethod, KeyTransportMethod);
            }

            if (SignResponse)
            {
                if (ResponseIssuer == null)
                {
                    throw new InvalidOperationException("A signed response needs an Issuer to place the signature after.");
                }

                FakeXmlSigner.Sign(response, SigningCertificate, SignatureMethod, DigestMethod);
            }

            return document;
        }

        /// <summary>
        /// Builds the response and encodes it as the base64 <c>SAMLResponse</c> form value.
        /// </summary>
        public string ToFormValue()
        {
            return Encode(BuildDocument());
        }

        private XmlElement AppendAssertion(XmlElement response)
        {
            XmlElement assertion = AppendElement(response, "saml", AssertionElementName, AssertionNamespace);
            // Declared on the assertion as well, so it stays self-contained once encrypted.
            assertion.SetAttribute("xmlns:saml", AssertionNamespace);
            assertion.SetAttribute("ID", AssertionId);
            assertion.SetAttribute("Version", "2.0");
            assertion.SetAttribute("IssueInstant", FormatTime(IssueInstant));
            AppendAssertionElement(assertion, IssuerElementName, AssertionIssuer);

            XmlElement subject = AppendAssertionElement(assertion, "Subject");
            XmlElement nameId = AppendAssertionElement(subject, "NameID", NameId);
            nameId.SetAttribute("Format", NameIdFormat);
            SetOptionalAttribute(nameId, "NameQualifier", NameQualifier);
            SetOptionalAttribute(nameId, "SPNameQualifier", ServiceProviderNameQualifier);

            XmlElement confirmation = AppendAssertionElement(subject, "SubjectConfirmation");
            confirmation.SetAttribute("Method", BearerMethod);
            XmlElement confirmationData = AppendAssertionElement(confirmation, "SubjectConfirmationData");
            SetOptionalAttribute(confirmationData, "InResponseTo", ConfirmationInResponseTo);
            SetOptionalAttribute(confirmationData, "Recipient", Recipient);
            SetOptionalTime(confirmationData, "NotBefore", ConfirmationNotBefore);
            SetOptionalTime(confirmationData, "NotOnOrAfter", ConfirmationNotOnOrAfter);

            XmlElement conditions = AppendAssertionElement(assertion, "Conditions");
            SetOptionalTime(conditions, "NotBefore", ConditionsNotBefore);
            SetOptionalTime(conditions, "NotOnOrAfter", ConditionsNotOnOrAfter);
            foreach (List<string> audiences in AudienceRestrictions)
            {
                XmlElement restriction = AppendAssertionElement(conditions, "AudienceRestriction");
                foreach (string audience in audiences)
                {
                    AppendAssertionElement(restriction, "Audience", audience);
                }
            }

            XmlElement authnStatement = AppendAssertionElement(assertion, "AuthnStatement");
            authnStatement.SetAttribute("AuthnInstant", FormatTime(AuthenticationInstant ?? IssueInstant));
            authnStatement.SetAttribute("SessionIndex", "_session-" + AssertionId);
            XmlElement authnContext = AppendAssertionElement(authnStatement, "AuthnContext");
            if (AuthnContextClassRef != null)
            {
                AppendAssertionElement(authnContext, "AuthnContextClassRef", AuthnContextClassRef);
            }

            if (AuthnContextDeclRef != null)
            {
                AppendAssertionElement(authnContext, "AuthnContextDeclRef", AuthnContextDeclRef);
            }

            if (Attributes.Count > 0)
            {
                XmlElement attributeStatement = AppendAssertionElement(assertion, "AttributeStatement");
                foreach (KeyValuePair<string, List<string>> attribute in Attributes)
                {
                    XmlElement attributeElement = AppendAssertionElement(attributeStatement, "Attribute");
                    attributeElement.SetAttribute("Name", attribute.Key);
                    foreach (string value in attribute.Value)
                    {
                        AppendAssertionElement(attributeElement, "AttributeValue", value);
                    }
                }
            }

            return assertion;
        }

        private static XmlElement AppendAssertionElement(XmlElement parent, string localName, string? text = null)
        {
            XmlElement element = AppendElement(parent, "saml", localName, AssertionNamespace);
            if (text != null)
            {
                element.InnerText = text;
            }

            return element;
        }

        private static XmlElement AppendElement(XmlElement parent, string prefix, string localName, string namespaceUri)
        {
            XmlElement element = parent.OwnerDocument.CreateElement(prefix, localName, namespaceUri);
            parent.AppendChild(element);
            return element;
        }

        private static void SetOptionalAttribute(XmlElement element, string name, string? value)
        {
            if (value != null)
            {
                element.SetAttribute(name, value);
            }
        }

        private static void SetOptionalTime(XmlElement element, string name, DateTimeOffset? value)
        {
            if (value is DateTimeOffset time)
            {
                element.SetAttribute(name, FormatTime(time));
            }
        }

        private static string FormatTime(DateTimeOffset time)
        {
            return time.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
    }
}
