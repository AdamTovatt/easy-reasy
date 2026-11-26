using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Schemas;
using Microsoft.IdentityModel.Tokens.Saml2;
using Microsoft.IdentityModel.Xml;
using System.Collections.Specialized;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Security;
using System.Text;
using System.Xml;
using EncryptedXml = System.Security.Cryptography.Xml.EncryptedXml;
using ItfoxtecSaml2Constants = ITfoxtec.Identity.Saml2.Schemas.Saml2Constants;
using SamlHttpRequest = ITfoxtec.Identity.Saml2.Http.HttpRequest;
using XmlException = System.Xml.XmlException;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// Validates the SAML response an identity provider posts to the assertion consumer service. ITfoxtec parses it,
    /// refuses DTDs, decrypts it and verifies its signatures against the pinned certificates; this class then checks,
    /// whatever the library also checks, that it answers the expected request, is addressed to this service provider,
    /// comes from the expected identity provider, carries a signed assertion with allowed algorithms that states an
    /// authentication and names a subject, and is within its times. Single use is not checked here: the request a
    /// response answers is consumed by whoever calls this.
    /// </summary>
    internal sealed class SamlResponseValidator
    {
        /// <summary>
        /// How far the identity provider's clock may be from this server's when checking the assertion's times.
        /// </summary>
        internal static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(2);

        private const string BearerConfirmationMethod = "urn:oasis:names:tc:SAML:2.0:cm:bearer";
        private const string AssertionNamespace = "urn:oasis:names:tc:SAML:2.0:assertion";
        private const string AssertionElementName = "Assertion";

        private readonly SamlServiceProviderSettings _serviceProvider;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="SamlResponseValidator"/> class.
        /// </summary>
        /// <param name="serviceProvider">This app as a service provider.</param>
        /// <param name="timeProvider">The clock the assertion's times are checked against.</param>
        public SamlResponseValidator(SamlServiceProviderSettings serviceProvider, TimeProvider timeProvider)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(timeProvider);

            _serviceProvider = serviceProvider;
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Validates a response.
        /// </summary>
        /// <param name="samlResponse">The base64 <c>SAMLResponse</c> form value, as posted.</param>
        /// <param name="expectedRequestId">The ID of the AuthnRequest this server sent, which the response must answer.</param>
        /// <param name="identityProvider">The identity provider that request named.</param>
        /// <returns>What the verified assertion says.</returns>
        /// <exception cref="SamlResponseRejectedException">Any check failed.</exception>
        public SamlValidatedAssertion Validate(string samlResponse, string expectedRequestId, SamlIdentityProviderTrust identityProvider)
        {
            ArgumentNullException.ThrowIfNull(samlResponse);
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedRequestId);
            ArgumentNullException.ThrowIfNull(identityProvider);

            if (_serviceProvider.DecryptionCertificates.Count > 0)
            {
                RequireAllowedEncryptionAlgorithms(samlResponse);
            }

            Saml2AuthnResponse response = ReadWithLibrary(samlResponse, identityProvider);
            DateTimeOffset now = _timeProvider.GetUtcNow();

            if (response.Status != Saml2StatusCodes.Success)
            {
                throw new SamlResponseRejectedException($"The response's status is {response.Status}, not Success.");
            }

            RequireEqual("response's InResponseTo", response.InResponseTo?.Value, expectedRequestId);
            RequireSameUrl("response's Destination", response.Destination, _serviceProvider.AssertionConsumerServiceUrl);
            RequireEqual("response's Issuer", response.Issuer, identityProvider.EntityId);

            Saml2Assertion assertion = response.Saml2SecurityToken.Assertion;
            RequireEqual("assertion's Issuer", assertion.Issuer?.Value, identityProvider.EntityId);
            RequireSignedAssertion(assertion);
            RequireEncryptedAssertionWhenDecrypting(response.XmlDocument);
            RequireBearerConfirmation(assertion.Subject, expectedRequestId, now);
            RequireConditions(assertion.Conditions, now);
            RequireAuthenticationStatement(assertion);

            Saml2NameIdentifier nameId = assertion.Subject.NameId
                ?? throw new SamlResponseRejectedException("The assertion's subject has no NameID.");
            return new SamlValidatedAssertion
            {
                AssertionId = assertion.Id.Value,
                IdentityProviderEntityId = identityProvider.EntityId,
                NameId = nameId.Value,
                NameIdFormat = nameId.Format?.OriginalString,
                NameQualifier = nameId.NameQualifier,
                ServiceProviderNameQualifier = nameId.SPNameQualifier,
            };
        }

        private static void RequireAllowedEncryptionAlgorithms(string samlResponse)
        {
            // ITfoxtec decrypts every EncryptedData in the document, wherever it sits, before any signature is checked,
            // and accepts RSA PKCS#1 v1.5 and AES-CBC with no setting to refuse them. An attacker who starts a sign-in
            // can post altered ciphertexts as often as they like, so the algorithms are checked here, before ITfoxtec
            // sees the response: only authenticated encryption and OAEP leave nothing to probe.
            XmlDocument document = ParseWithoutDtd(samlResponse);
            foreach (XmlElement encryptedData in document.GetElementsByTagName("EncryptedData", EncryptedXml.XmlEncNamespaceUrl).OfType<XmlElement>())
            {
                RequireAllowed("data encryption method", EncryptionMethodOf(encryptedData), SamlAlgorithms.DataEncryptionMethods);
            }

            foreach (XmlElement encryptedKey in document.GetElementsByTagName("EncryptedKey", EncryptedXml.XmlEncNamespaceUrl).OfType<XmlElement>())
            {
                RequireAllowed("key transport method", EncryptionMethodOf(encryptedKey), SamlAlgorithms.KeyTransportMethods);
            }
        }

        private static XmlDocument ParseWithoutDtd(string samlResponse)
        {
            try
            {
                string xml = Encoding.UTF8.GetString(Convert.FromBase64String(samlResponse));
                XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using StringReader stringReader = new StringReader(xml);
                using XmlReader reader = XmlReader.Create(stringReader, settings);
                XmlDocument document = new XmlDocument { XmlResolver = null };
                document.Load(reader);
                return document;
            }
            catch (Exception exception) when (exception is FormatException or XmlException)
            {
                throw new SamlResponseRejectedException("The response is not base64-encoded XML without a DTD.");
            }
        }

        private static string? EncryptionMethodOf(XmlElement encrypted)
        {
            XmlElement? encryptionMethod = encrypted["EncryptionMethod", EncryptedXml.XmlEncNamespaceUrl];
            return encryptionMethod?.GetAttribute("Algorithm");
        }

        private Saml2AuthnResponse ReadWithLibrary(string samlResponse, SamlIdentityProviderTrust identityProvider)
        {
            Saml2Configuration configuration = new Saml2Configuration
            {
                Issuer = _serviceProvider.EntityId,
                AllowedIssuer = identityProvider.EntityId,
                SignatureValidationCertificates = identityProvider.SigningCertificates.ToList(),
                SignatureValidationAlgorithms = SamlAlgorithms.SignatureMethods.ToList(),
                XmlCanonicalizationValidationMethods = SamlAlgorithms.CanonicalizationMethods.ToList(),
                // The certificates are pinned per identity provider, so trust comes from the pin, not from a chain.
                CertificateValidationMode = X509CertificateValidationMode.None,
                RevocationMode = X509RevocationMode.NoCheck,
                AudienceRestricted = true,
                AllowedAudienceUris = [_serviceProvider.EntityId],
                DecryptionCertificates = _serviceProvider.DecryptionCertificates.ToList(),
            };

            SamlHttpRequest request = new SamlHttpRequest
            {
                Method = "POST",
                Form = new NameValueCollection { { ItfoxtecSaml2Constants.Message.SamlResponse, samlResponse } },
            };

            try
            {
                Saml2AuthnResponse response = new Saml2AuthnResponse(configuration);
                new Saml2PostBinding().Unbind(request, response);
                return response;
            }
            catch (Exception exception)
            {
                // The library's message stays on the inner exception, for logs: whether decryption or a signature
                // failed is what a decryption oracle reads, so the refusal itself says neither.
                throw new SamlResponseRejectedException("ITfoxtec refused the response.", exception);
            }
        }

        private static void RequireSignedAssertion(Saml2Assertion assertion)
        {
            // The library has verified every signature that is present and accepts a signed response in place of a
            // signed assertion. Only the assertion's own signature counts here, so the identity read from it is covered
            // by a signature whatever wraps it.
            Signature? signature = assertion.Signature;
            if (signature == null)
            {
                throw new SamlResponseRejectedException("The assertion is not signed.");
            }

            SignedInfo signedInfo = signature.SignedInfo;
            RequireAllowed("assertion signature's signature method", signedInfo.SignatureMethod, SamlAlgorithms.SignatureMethods);
            RequireAllowed("assertion signature's canonicalization method", signedInfo.CanonicalizationMethod, SamlAlgorithms.CanonicalizationMethods);
            if (signedInfo.References.Count != 1)
            {
                throw new SamlResponseRejectedException($"The assertion's signature has {signedInfo.References.Count} references, not one.");
            }

            Reference reference = signedInfo.References[0];
            RequireEqual("assertion signature's reference", reference.Uri, "#" + assertion.Id.Value);
            // ITfoxtec checks the signature method against its allowlist but never a reference's DigestMethod, so a
            // SHA-1 digest under an RSA-SHA256 signature passes it; this check is the only one that refuses it.
            RequireAllowed("assertion signature's digest method", reference.DigestMethod, SamlAlgorithms.DigestMethods);
        }

        private void RequireEncryptedAssertionWhenDecrypting(XmlDocument document)
        {
            if (_serviceProvider.DecryptionCertificates.Count == 0)
            {
                return;
            }

            // The library leaves each decrypted assertion inside its EncryptedAssertion, so an Assertion directly in the
            // response arrived in plaintext.
            bool hasPlaintextAssertion = document.DocumentElement!.ChildNodes
                .OfType<XmlElement>()
                .Any(element => element.LocalName == AssertionElementName && element.NamespaceURI == AssertionNamespace);
            if (hasPlaintextAssertion)
            {
                throw new SamlResponseRejectedException("The assertion arrived in plaintext, but this service provider decrypts assertions.");
            }
        }

        private void RequireBearerConfirmation(Saml2Subject subject, string expectedRequestId, DateTimeOffset now)
        {
            List<Saml2SubjectConfirmation> bearerConfirmations = subject.SubjectConfirmations
                .Where(confirmation => confirmation.Method?.OriginalString == BearerConfirmationMethod)
                .ToList();
            if (bearerConfirmations.Count != 1)
            {
                throw new SamlResponseRejectedException($"The subject has {bearerConfirmations.Count} bearer confirmations, not one.");
            }

            Saml2SubjectConfirmationData? data = bearerConfirmations[0].SubjectConfirmationData;
            if (data == null)
            {
                throw new SamlResponseRejectedException("The bearer confirmation has no SubjectConfirmationData.");
            }

            RequireSameUrl("bearer confirmation's Recipient", data.Recipient, _serviceProvider.AssertionConsumerServiceUrl);
            RequireEqual("bearer confirmation's InResponseTo", data.InResponseTo?.Value, expectedRequestId);
            if (data.NotBefore != null)
            {
                throw new SamlResponseRejectedException("The bearer confirmation has a NotBefore, which a bearer confirmation must not have.");
            }

            RequireNotExpired("bearer confirmation", data.NotOnOrAfter, now);
        }

        private void RequireConditions(Saml2Conditions? conditions, DateTimeOffset now)
        {
            if (conditions == null)
            {
                throw new SamlResponseRejectedException("The assertion has no Conditions.");
            }

            if (conditions.NotBefore is DateTime notBefore && now < ToUtc(notBefore) - AllowedClockSkew)
            {
                throw new SamlResponseRejectedException($"The assertion is not valid before {ToUtc(notBefore):O}.");
            }

            RequireNotExpired("assertion's conditions", conditions.NotOnOrAfter, now);

            // Each AudienceRestriction is a condition of its own, so every one must name this service provider.
            if (conditions.AudienceRestrictions.Count == 0)
            {
                throw new SamlResponseRejectedException("The assertion has no AudienceRestriction.");
            }

            foreach (Saml2AudienceRestriction restriction in conditions.AudienceRestrictions)
            {
                if (!restriction.Audiences.Contains(_serviceProvider.EntityId, StringComparer.Ordinal))
                {
                    throw new SamlResponseRejectedException($"An AudienceRestriction does not name this service provider ({string.Join(", ", restriction.Audiences)}).");
                }
            }
        }

        private static void RequireAuthenticationStatement(Saml2Assertion assertion)
        {
            // The web browser SSO profile requires an AuthnStatement in the bearer assertion a sign-in rests on.
            if (!assertion.Statements.OfType<Saml2AuthenticationStatement>().Any())
            {
                throw new SamlResponseRejectedException("The assertion has no AuthnStatement.");
            }
        }

        private static void RequireNotExpired(string what, DateTime? notOnOrAfter, DateTimeOffset now)
        {
            if (notOnOrAfter is not DateTime expiry)
            {
                throw new SamlResponseRejectedException($"No NotOnOrAfter on the {what}.");
            }

            if (now >= ToUtc(expiry) + AllowedClockSkew)
            {
                throw new SamlResponseRejectedException($"The {what} expired at {ToUtc(expiry):O}.");
            }
        }

        private static void RequireEqual(string what, string? actual, string expected)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new SamlResponseRejectedException($"The {what} is '{actual}', not '{expected}'.");
            }
        }

        private static void RequireSameUrl(string what, Uri? actual, Uri expected)
        {
            if (actual == null || !actual.IsAbsoluteUri || !string.Equals(actual.AbsoluteUri, expected.AbsoluteUri, StringComparison.Ordinal))
            {
                throw new SamlResponseRejectedException($"The {what} is '{actual}', not '{expected}'.");
            }
        }

        private static void RequireAllowed(string what, string? actual, IReadOnlyList<string> allowed)
        {
            if (actual == null || !allowed.Contains(actual, StringComparer.Ordinal))
            {
                throw new SamlResponseRejectedException($"The {what} '{actual}' is not allowed.");
            }
        }

        private static DateTimeOffset ToUtc(DateTime time)
        {
            DateTime utc = time.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : time.ToUniversalTime();
            return new DateTimeOffset(utc);
        }
    }
}
