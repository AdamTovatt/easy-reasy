using System.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// Hostile XML: document type declarations are refused before any entity is expanded or fetched, and a comment
    /// inside a signed value cannot cut the value short.
    /// </summary>
    [TestClass]
    public class SamlXmlParsingTests
    {
        [TestMethod]
        public void Validate_InternalEntityInNameId_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            string xml = harness.Respond("user-1234").BuildDocument().OuterXml;
            string hostile = "<!DOCTYPE samlp:Response [<!ENTITY victim \"victim-1\">]>" + xml.Replace(">user-1234<", ">&victim;<", StringComparison.Ordinal);

            SamlResponseRejectedException rejection = harness.Reject(FakeSamlResponse.Encode(hostile));

            SamlRejectionAssert.ByLibrary<XmlException>(rejection, "DTD");
        }

        [TestMethod]
        public void Validate_ExternalEntity_IsRefusedAtTheDoctype()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            string xml = harness.Respond("user-1234").BuildDocument().OuterXml;
            string hostile = "<!DOCTYPE samlp:Response [<!ENTITY secret SYSTEM \"file:///etc/hostname\">]>" + xml.Replace(">user-1234<", ">&secret;<", StringComparison.Ordinal);

            SamlResponseRejectedException rejection = harness.Reject(FakeSamlResponse.Encode(hostile));

            SamlRejectionAssert.ByLibrary<XmlException>(rejection, "DTD");
        }

        [TestMethod]
        public void Validate_EntityExpansionBomb_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            string xml = harness.Respond("user-1234").BuildDocument().OuterXml;
            string entities = "<!ENTITY lol0 \"lol\">" + string.Concat(Enumerable.Range(1, 9).Select(level =>
                $"<!ENTITY lol{level} \"{string.Concat(Enumerable.Repeat($"&lol{level - 1};", 10))}\">"));
            string hostile = $"<!DOCTYPE samlp:Response [{entities}]>" + xml.Replace(">user-1234<", ">&lol9;<", StringComparison.Ordinal);

            SamlResponseRejectedException rejection = harness.Reject(FakeSamlResponse.Encode(hostile));

            SamlRejectionAssert.ByLibrary<XmlException>(rejection, "DTD");
        }

        [TestMethod]
        public void Validate_DoctypeWithoutEntities_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            string hostile = "<!DOCTYPE samlp:Response>" + harness.Respond().BuildDocument().OuterXml;

            SamlResponseRejectedException rejection = harness.Reject(FakeSamlResponse.Encode(hostile));

            SamlRejectionAssert.ByLibrary<XmlException>(rejection, "DTD");
        }

        [TestMethod]
        public void Validate_CommentInsertedIntoSignedNameId_ReturnsTheWholeSignedValue()
        {
            // Exclusive canonicalization drops comments, so the signature still verifies after the attacker inserts
            // one. A parser that reads only the first text node would see "victim@example.test"; the whole value
            // must come back instead.
            SamlValidationHarness harness = new SamlValidationHarness();
            XmlDocument document = harness.Respond("victim@example.test.attacker.test").BuildDocument();
            XmlElement nameId = SamlXmlEdit.NameId(SamlXmlEdit.Assertion(document));
            nameId.InnerXml = "victim@example.test<!---->.attacker.test";

            SamlValidatedAssertion result = harness.Validate(FakeSamlResponse.Encode(document));

            Assert.AreEqual("victim@example.test.attacker.test", result.NameId);
        }
    }
}
