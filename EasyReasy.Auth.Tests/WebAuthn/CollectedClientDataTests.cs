using System.Text;
using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class CollectedClientDataTests
    {
        [TestMethod]
        public void Parse_ValidClientData_ReadsTypeChallengeAndOrigin()
        {
            // All three values are distinct and none is a substring of another, so a parser that read the
            // wrong field into any of them fails this.
            string json = """{"type":"webauthn.get","challenge":"Q0hBTExFTkdF","origin":"https://example.com"}""";

            CollectedClientData parsed = Parse(json);

            Assert.AreEqual("webauthn.get", parsed.Type);
            Assert.AreEqual("Q0hBTExFTkdF", parsed.Challenge);
            Assert.AreEqual("https://example.com", parsed.Origin);
        }

        [TestMethod]
        public void Parse_FieldsInAnotherOrderWithExtrasAlongside_ReadsTheSameThreeValues()
        {
            // Browsers are free to add fields and to order them as they like; clientDataJSON is signed as
            // received, so the parser has to take it as it comes.
            string json = """
                {"crossOrigin":false,"origin":"https://example.com","tokenBinding":{"status":"supported"},"challenge":"Q0hBTExFTkdF","type":"webauthn.create"}
                """;

            CollectedClientData parsed = Parse(json);

            Assert.AreEqual("webauthn.create", parsed.Type);
            Assert.AreEqual("Q0hBTExFTkdF", parsed.Challenge);
            Assert.AreEqual("https://example.com", parsed.Origin);
        }

        [TestMethod]
        public void Parse_CrossOriginTrue_ReportsIt()
        {
            string json = """{"type":"webauthn.get","challenge":"Q0hBTExFTkdF","origin":"https://example.com","crossOrigin":true}""";

            Assert.IsTrue(Parse(json).CrossOrigin);
        }

        [TestMethod]
        public void Parse_CrossOriginFalse_ReportsIt()
        {
            string json = """{"type":"webauthn.get","challenge":"Q0hBTExFTkdF","origin":"https://example.com","crossOrigin":false}""";

            Assert.IsFalse(Parse(json).CrossOrigin);
        }

        [TestMethod]
        public void Parse_NoCrossOriginField_ReportsNotCrossOrigin()
        {
            // Absent means a same-origin ceremony; reading it as cross-origin would reject every browser
            // that leaves the field out.
            string json = """{"type":"webauthn.get","challenge":"Q0hBTExFTkdF","origin":"https://example.com"}""";

            Assert.IsFalse(Parse(json).CrossOrigin);
        }

        [TestMethod]
        public void Parse_CrossOriginThatIsNotABoolean_Throws()
        {
            string json = """{"type":"webauthn.get","challenge":"Q0hBTExFTkdF","origin":"https://example.com","crossOrigin":"yes"}""";

            WebAuthnParseException exception = AssertParseError(json);
            StringAssert.Contains(exception.Message, "not a boolean");
        }

        [DataTestMethod]
        [DataRow("""{"challenge":"Q0hBTExFTkdF","origin":"https://example.com"}""", "type")]
        [DataRow("""{"type":"webauthn.get","origin":"https://example.com"}""", "challenge")]
        [DataRow("""{"type":"webauthn.get","challenge":"Q0hBTExFTkdF"}""", "origin")]
        public void Parse_MissingARequiredField_Throws(string json, string missingField)
        {
            WebAuthnParseException exception = AssertParseError(json);
            StringAssert.Contains(exception.Message, $"\"{missingField}\"");
        }

        [DataTestMethod]
        [DataRow("""{"type":"","challenge":"Q0hBTExFTkdF","origin":"https://example.com"}""", "type")]
        [DataRow("""{"type":"webauthn.get","challenge":"","origin":"https://example.com"}""", "challenge")]
        [DataRow("""{"type":"webauthn.get","challenge":"Q0hBTExFTkdF","origin":""}""", "origin")]
        public void Parse_EmptyRequiredField_Throws(string json, string emptyField)
        {
            WebAuthnParseException exception = AssertParseError(json);
            StringAssert.Contains(exception.Message, $"empty \"{emptyField}\"");
        }

        [DataTestMethod]
        [DataRow("""{"type":42,"challenge":"Q0hBTExFTkdF","origin":"https://example.com"}""")]
        [DataRow("""{"type":null,"challenge":"Q0hBTExFTkdF","origin":"https://example.com"}""")]
        [DataRow("""{"type":{"a":1},"challenge":"Q0hBTExFTkdF","origin":"https://example.com"}""")]
        public void Parse_RequiredFieldThatIsNotAString_Throws(string json)
        {
            AssertParseError(json);
        }

        [DataTestMethod]
        [DataRow("[]")]
        [DataRow("\"webauthn.get\"")]
        [DataRow("null")]
        public void Parse_JsonThatIsNotAnObject_Throws(string json)
        {
            AssertParseError(json);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("{")]
        [DataRow("not json at all")]
        public void Parse_BytesThatAreNotJson_Throws(string json)
        {
            AssertParseError(json);
        }

        [TestMethod]
        public void Parse_BytesThatAreNotValidUtf8_Throws()
        {
            // Parse takes raw bytes because those are the bytes the signature covers, so it has to survive
            // a byte sequence that is not text at all.
            byte[] invalidUtf8 = new byte[] { (byte)'{', (byte)'"', 0xC3, 0x28, (byte)'"', (byte)'}' };

            WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedClientData, () => CollectedClientData.Parse(invalidUtf8));
        }

        [DataTestMethod]
        [DataRow("type")]
        [DataRow("challenge")]
        [DataRow("origin")]
        public void Parse_FieldCarryingANewline_IsRejected(string fieldName)
        {
            // These three are the only attacker-controlled values the verifier puts into the failure
            // message it hands back, and that message is documented as one to log. A newline in one of them
            // is a rejected ceremony choosing what extra lines appear in the application's log.
            string json = NewClientDataJson(fieldName, "webauthn.create\nforged log line");

            WebAuthnParseException exception = AssertParseError(json);

            StringAssert.Contains(exception.Message, "control character");
        }

        [DataTestMethod]
        [DataRow("type")]
        [DataRow("challenge")]
        [DataRow("origin")]
        public void Parse_FieldLongerThanAnyLegitimateValue_IsRejected(string fieldName)
        {
            string json = NewClientDataJson(fieldName, new string('a', 1025));

            AssertParseError(json);
        }

        [DataTestMethod]
        [DataRow("type")]
        [DataRow("challenge")]
        [DataRow("origin")]
        public void Parse_FieldAtTheLengthLimit_IsAccepted(string fieldName)
        {
            // Pins which side of the bound is rejected; without it the cap could be off by one in the
            // direction that refuses legitimate client data and nothing would say so.
            string atTheLimit = new string('a', 1024);
            CollectedClientData clientData = Parse(NewClientDataJson(fieldName, atTheLimit));

            // Asserts the field came through rather than only that parsing did not throw: the return is
            // non-nullable, so a null check could never fail whatever the parser stored.
            string kept = fieldName switch
            {
                "type" => clientData.Type,
                "challenge" => clientData.Challenge,
                _ => clientData.Origin,
            };

            Assert.AreEqual(atTheLimit, kept);
        }

        /// <summary>
        /// Builds client data that is correct except for the named field, which carries the given value.
        /// </summary>
        private static string NewClientDataJson(string fieldName, string value)
        {
            string type = fieldName == "type" ? value : "webauthn.create";
            string challenge = fieldName == "challenge" ? value : "Y2hhbGxlbmdl";
            string origin = fieldName == "origin" ? value : WebAuthnTestData.Origin;

            return JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["type"] = type,
                ["challenge"] = challenge,
                ["origin"] = origin,
            });
        }

        private static CollectedClientData Parse(string json)
        {
            return CollectedClientData.Parse(Encoding.UTF8.GetBytes(json));
        }

        private static WebAuthnParseException AssertParseError(string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            return WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedClientData, () => CollectedClientData.Parse(bytes));
        }
    }
}
