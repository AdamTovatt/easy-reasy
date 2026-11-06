namespace EasyReasy.Auth.Microsoft.Tests
{
    [TestClass]
    public class MicrosoftSignInRequestTests
    {
        [TestMethod]
        public void ToJson_SerializesCodeAndState()
        {
            MicrosoftSignInRequest request = new MicrosoftSignInRequest("the-code", "the-state");

            Assert.AreEqual("{\"code\":\"the-code\",\"state\":\"the-state\"}", request.ToJson());
        }

        [TestMethod]
        public void FromJson_RoundTrip_PreservesValues()
        {
            MicrosoftSignInRequest request = MicrosoftSignInRequest.FromJson(new MicrosoftSignInRequest("the-code", "the-state").ToJson());

            Assert.AreEqual("the-code", request.Code);
            Assert.AreEqual("the-state", request.State);
        }

        [TestMethod]
        public void ToString_RedactsTheCodeAndTheState()
        {
            string text = new MicrosoftSignInRequest("redeemable-code", "sealed-state").ToString();

            Assert.AreEqual("{\"code\":\"[REDACTED]\",\"state\":\"[REDACTED]\"}", text);
            Assert.IsFalse(text.Contains("redeemable-code"));
            Assert.IsFalse(text.Contains("sealed-state"));
        }

        [TestMethod]
        [DataRow("not valid json")]
        [DataRow("null")]
        public void FromJson_InvalidOrNullJson_ThrowsArgumentException(string json)
        {
            Assert.ThrowsException<ArgumentException>(() => MicrosoftSignInRequest.FromJson(json));
        }
    }
}
