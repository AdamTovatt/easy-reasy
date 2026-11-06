namespace EasyReasy.Auth.Microsoft.Tests
{
    [TestClass]
    public class MicrosoftSignInBeginResponseTests
    {
        private const string AuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/authorize?client_id=x";

        [TestMethod]
        public void ToJson_SerializesTheAuthorizationUrl()
        {
            MicrosoftSignInBeginResponse response = new MicrosoftSignInBeginResponse(AuthorizationUrl);

            StringAssert.StartsWith(response.ToJson(), "{\"authorizationUrl\":");
            Assert.AreEqual(response.ToJson(), response.ToString());
        }

        [TestMethod]
        public void FromJson_RoundTrip_PreservesTheAuthorizationUrl()
        {
            MicrosoftSignInBeginResponse response = MicrosoftSignInBeginResponse.FromJson(new MicrosoftSignInBeginResponse(AuthorizationUrl).ToJson());

            Assert.AreEqual(AuthorizationUrl, response.AuthorizationUrl);
        }

        [TestMethod]
        [DataRow("not valid json")]
        [DataRow("null")]
        [DataRow("{}")]
        public void FromJson_InvalidNullOrWithoutAUrl_ThrowsArgumentException(string json)
        {
            Assert.ThrowsException<ArgumentException>(() => MicrosoftSignInBeginResponse.FromJson(json));
        }
    }
}
