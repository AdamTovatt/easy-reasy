using Microsoft.IdentityModel.JsonWebTokens;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// The two issuer forms, each checked when built, and which issuers each can stand for.
    /// </summary>
    [TestClass]
    public class OpenIdConnectIssuerTests
    {
        private const string Template = "https://login.example/{tenantid}/v2.0";
        private const string MetadataAddress = "https://login.example/organizations/v2.0/.well-known/openid-configuration";

        [TestMethod]
        [DataRow("https://issuer.example/realms/a", "https://issuer.example/realms/a/.well-known/openid-configuration")]
        [DataRow("https://issuer.example/realms/a/", "https://issuer.example/realms/a/.well-known/openid-configuration")]
        public void Fixed_ValidIssuer_ReadsDiscoveryFromItsWellKnownDocument(string issuer, string expected)
        {
            OpenIdConnectIssuer fixedIssuer = OpenIdConnectIssuer.Fixed(issuer);

            Assert.AreEqual(issuer, fixedIssuer.Value);
            Assert.AreEqual(expected, fixedIssuer.MetadataAddress);
            Assert.IsNull(fixedIssuer.TenantClaim);
        }

        [TestMethod]
        [DataRow("http://issuer.example")]
        [DataRow("https://issuer.example?tenant=a")]
        [DataRow("https://issuer.example#a")]
        [DataRow("issuer.example")]
        [DataRow("http://app.localhost")]
        public void Fixed_IssuerNotAnHttpsUrlWithoutQueryOrFragment_ThrowsNamingIt(string issuer)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => OpenIdConnectIssuer.Fixed(issuer));

            StringAssert.Contains(exception.Message, issuer);
        }

        [TestMethod]
        [DataRow("http://localhost:8080/realms/test")]
        [DataRow("http://127.0.0.1:8080")]
        public void Fixed_LoopbackHttpIssuer_Builds(string issuer)
        {
            Assert.AreEqual(issuer, OpenIdConnectIssuer.Fixed(issuer).Value);
        }

        [TestMethod]
        [DataRow("https://issuer.example", true)]
        [DataRow("https://issuer.example/", false)]
        [DataRow("https://ISSUER.example", false)]
        public void CanIssueAs_FixedIssuer_IsExactlyItself(string issuer, bool expected)
        {
            Assert.AreEqual(expected, OpenIdConnectIssuer.Fixed("https://issuer.example").CanIssueAs(issuer));
        }

        [TestMethod]
        public void TenantTemplate_ValidParts_KeepsThem()
        {
            OpenIdConnectIssuer template = OpenIdConnectIssuer.TenantTemplate(Template, "tid", MetadataAddress);

            Assert.AreEqual(Template, template.Value);
            Assert.AreEqual(MetadataAddress, template.MetadataAddress);
            Assert.AreEqual("tid", template.TenantClaim);
        }

        [TestMethod]
        [DataRow("https://login.example/v2.0")]
        [DataRow("https://login.example/{tenantid}/{tenantid}/v2.0")]
        [DataRow("http://login.example/{tenantid}/v2.0")]
        [DataRow("https://login.example/{tenantid}/v2.0?x=1")]
        public void TenantTemplate_TemplateWithoutOnePlaceholderInAnIssuerUrl_ThrowsNamingIt(string template)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => OpenIdConnectIssuer.TenantTemplate(template, "tid", MetadataAddress));

            StringAssert.Contains(exception.Message, template);
        }

        [TestMethod]
        public void TenantTemplate_SeveralProblems_ReportsEveryOneInOneException()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => OpenIdConnectIssuer.TenantTemplate("https://login.example/v2.0", " ", "http://login.example/doc"));

            StringAssert.Contains(exception.Message, "https://login.example/v2.0");
            StringAssert.Contains(exception.Message, "tenant claim");
            StringAssert.Contains(exception.Message, "http://login.example/doc");
        }

        [TestMethod]
        [DataRow("https://login.example/tenant-a/v2.0", true)]
        [DataRow("https://login.example/{tenantid}/v2.0", false)]
        [DataRow("https://login.example//v2.0", false)]
        [DataRow("https://login.example/a/b/v2.0", false)]
        [DataRow("https://other.example/tenant-a/v2.0", false)]
        [DataRow("https://login.example/tenant-a/v1.0", false)]
        public void CanIssueAs_Template_IsTheTemplateWithOneTenantFilledIn(string issuer, bool expected)
        {
            Assert.AreEqual(expected, OpenIdConnectIssuer.TenantTemplate(Template, "tid", MetadataAddress).CanIssueAs(issuer));
        }

        [TestMethod]
        public void Fixed_IssuerHoldingThePlaceholder_ThrowsPointingAtTheTemplate()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => OpenIdConnectIssuer.Fixed(Template));

            StringAssert.Contains(exception.Message, nameof(OpenIdConnectIssuer.TenantTemplate));
        }

        [TestMethod]
        public void CanIssueAs_Null_IsFalseForEitherForm()
        {
            Assert.IsFalse(OpenIdConnectIssuer.Fixed("https://issuer.example").CanIssueAs(null));
            Assert.IsFalse(OpenIdConnectIssuer.TenantTemplate(Template, "tid", MetadataAddress).CanIssueAs(null));
        }

        [TestMethod]
        [DataRow("https://login.example/tenant-a/v2.0", "tenant-a", true)]
        [DataRow("https://login.example/tenant-b/v2.0", "tenant-a", false)]
        [DataRow("https://login.example//v2.0", "", false)]
        [DataRow("https://login.example/a/b/v2.0", "a/b", false)]
        [DataRow("https://login.example/{x}/v2.0", "{x}", false)]
        public void IsTokenIssuer_Template_IsTheTemplateWithTheTokensOwnTenantId(string issuer, string tenantId, bool expected)
        {
            JsonWebToken token = CreateToken($"{{\"tid\":\"{tenantId}\"}}");

            Assert.AreEqual(expected, OpenIdConnectIssuer.TenantTemplate(Template, "tid", MetadataAddress).IsTokenIssuer(issuer, token));
        }

        [TestMethod]
        public void IsTokenIssuer_TemplateAndATokenWithoutTheTenantClaim_IsFalse()
        {
            Assert.IsFalse(OpenIdConnectIssuer.TenantTemplate(Template, "tid", MetadataAddress).IsTokenIssuer("https://login.example/tenant-a/v2.0", CreateToken("{}")));
        }

        [TestMethod]
        [DataRow("https://issuer.example", true)]
        [DataRow("https://issuer.example/", false)]
        public void IsTokenIssuer_FixedIssuer_IsExactlyItselfWhateverTheTokenHolds(string issuer, bool expected)
        {
            JsonWebToken token = CreateToken("{\"tid\":\"tenant-a\"}");

            Assert.AreEqual(expected, OpenIdConnectIssuer.Fixed("https://issuer.example").IsTokenIssuer(issuer, token));
        }

        private static JsonWebToken CreateToken(string payload)
        {
            return new JsonWebToken("{\"alg\":\"none\"}", payload);
        }
    }
}
