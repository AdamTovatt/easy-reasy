namespace EasyReasy.Auth.Microsoft.Tests
{
    [TestClass]
    public class MicrosoftSignInBeginRequestTests
    {
        [TestMethod]
        public void ToJson_SerializesTheLoginHint()
        {
            MicrosoftSignInBeginRequest request = new MicrosoftSignInBeginRequest("ada@contoso.example");

            Assert.AreEqual("{\"loginHint\":\"ada@contoso.example\"}", request.ToJson());
            Assert.AreEqual(request.ToJson(), request.ToString());
        }

        [TestMethod]
        [DataRow("ada@contoso.example")]
        [DataRow(null)]
        public void FromJson_RoundTrip_PreservesTheLoginHint(string? loginHint)
        {
            MicrosoftSignInBeginRequest request = MicrosoftSignInBeginRequest.FromJson(new MicrosoftSignInBeginRequest(loginHint).ToJson());

            Assert.AreEqual(loginHint, request.LoginHint);
        }

        [TestMethod]
        [DataRow("not valid json")]
        [DataRow("null")]
        public void FromJson_InvalidOrNullJson_ThrowsArgumentException(string json)
        {
            Assert.ThrowsException<ArgumentException>(() => MicrosoftSignInBeginRequest.FromJson(json));
        }
    }
}
