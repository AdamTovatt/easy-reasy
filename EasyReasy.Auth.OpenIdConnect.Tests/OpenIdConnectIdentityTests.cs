using System.Text.Json;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    [TestClass]
    public class OpenIdConnectIdentityTests
    {
        [TestMethod]
        public void Equals_SameIssuerAndSubject_IsEqualWithTheSameHash()
        {
            OpenIdConnectIdentity first = new OpenIdConnectIdentity("https://issuer.example", "sub-1");
            OpenIdConnectIdentity second = new OpenIdConnectIdentity("https://issuer.example", "sub-1");

            Assert.IsTrue(first == second);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        }

        [TestMethod]
        [DataRow("https://other.example", "sub-1")]
        [DataRow("https://issuer.example", "SUB-1")]
        [DataRow("https://issuer.example/", "sub-1")]
        public void Equals_AnyPartDifferingEvenInCaseOrASlash_IsAnotherAccount(string issuer, string subject)
        {
            Assert.IsTrue(new OpenIdConnectIdentity("https://issuer.example", "sub-1") != new OpenIdConnectIdentity(issuer, subject));
        }

        [TestMethod]
        [DataRow("", "sub")]
        [DataRow(" ", "sub")]
        [DataRow("https://issuer.example", "")]
        public void Constructor_BlankPart_Throws(string issuer, string subject)
        {
            Assert.ThrowsException<ArgumentException>(() => new OpenIdConnectIdentity(issuer, subject));
        }

        [TestMethod]
        public void ToString_PartsHoldingQuotesAndSeparators_IsJsonThatReadsBackToThem()
        {
            OpenIdConnectIdentity identity = new OpenIdConnectIdentity("https://issuer.example/a#b", "sub\"with:|seps");

            using JsonDocument json = JsonDocument.Parse(identity.ToString());

            Assert.AreEqual(identity.Issuer, json.RootElement.GetProperty("issuer").GetString());
            Assert.AreEqual(identity.Subject, json.RootElement.GetProperty("subject").GetString());
        }
    }
}
