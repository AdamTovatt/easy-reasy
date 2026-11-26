namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// The endpoint bodies' JSON pattern, and the redaction of the code and state.
    /// </summary>
    [TestClass]
    public class OpenIdConnectModelSerializationTests
    {
        [TestMethod]
        public void AuthorizationResponse_FromJson_ReadsTheIssParameterAndTheRedirectUri()
        {
            OpenIdConnectAuthorizationResponse response = OpenIdConnectAuthorizationResponse.FromJson(
                "{\"code\":\"c\",\"state\":\"s\",\"iss\":\"https://issuer.example\",\"redirectUri\":\"https://app.example/callback\"}");

            Assert.AreEqual("c", response.Code);
            Assert.AreEqual("s", response.State);
            Assert.AreEqual("https://issuer.example", response.Issuer);
            Assert.AreEqual("https://app.example/callback", response.RedirectUri);
        }

        [TestMethod]
        public void AuthorizationResponse_ToJsonAndBack_KeepsEveryValue()
        {
            OpenIdConnectAuthorizationResponse original = new OpenIdConnectAuthorizationResponse("c", "s", null, "https://app.example/callback");

            OpenIdConnectAuthorizationResponse copy = OpenIdConnectAuthorizationResponse.FromJson(original.ToJson());

            Assert.AreEqual(original.Code, copy.Code);
            Assert.AreEqual(original.State, copy.State);
            Assert.IsNull(copy.Issuer);
            Assert.AreEqual(original.RedirectUri, copy.RedirectUri);
        }

        [TestMethod]
        public void AuthorizationResponse_ToString_RedactsTheCodeAndState()
        {
            string text = new OpenIdConnectAuthorizationResponse("secret-code", "secret-state", "https://issuer.example", "https://app.example/callback").ToString();

            Assert.IsFalse(text.Contains("secret-code", StringComparison.Ordinal), text);
            Assert.IsFalse(text.Contains("secret-state", StringComparison.Ordinal), text);
            StringAssert.Contains(text, "https://app.example/callback");
        }

        [TestMethod]
        [DataRow("not json")]
        [DataRow("null")]
        public void AuthorizationResponse_FromInvalidJson_ThrowsArgumentException(string json)
        {
            Assert.ThrowsException<ArgumentException>(() => OpenIdConnectAuthorizationResponse.FromJson(json));
        }

        [TestMethod]
        public void SignInBeginRequest_ToJsonAndBack_KeepsTheProviderAndLoginHint()
        {
            OpenIdConnectSignInBeginRequest copy = OpenIdConnectSignInBeginRequest.FromJson(new OpenIdConnectSignInBeginRequest("okta", "ada@example.test").ToJson());

            Assert.AreEqual("okta", copy.ProviderKey);
            Assert.AreEqual("ada@example.test", copy.LoginHint);
        }

        [TestMethod]
        public void SignInBeginResponse_ToJsonAndBack_KeepsTheAuthorizationUrl()
        {
            OpenIdConnectSignInBeginResponse copy = OpenIdConnectSignInBeginResponse.FromJson(new OpenIdConnectSignInBeginResponse("https://issuer.example/authorize?x=1").ToJson());

            Assert.AreEqual("https://issuer.example/authorize?x=1", copy.AuthorizationUrl);
        }

        [TestMethod]
        public void SignInBeginResponse_WithoutAnAuthorizationUrl_ThrowsArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(() => OpenIdConnectSignInBeginResponse.FromJson("{}"));
        }
    }
}
