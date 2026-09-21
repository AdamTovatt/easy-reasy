using System.Net;

namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// The wrong-host case as it actually arrives — a real handler following a real redirect to a real sign-in page.
    /// This is the case the reported failure has to name, and the one a fake handler cannot produce.
    /// </summary>
    [TestClass]
    public class WrongHostRedirectTests
    {
        [TestMethod]
        public async Task GetAsync_ApiKey_HostRedirectsTheAuthPathToItsSignInPage_ReportsTheUriTheRequestEndedAt()
        {
            // Arrange
            using SignInPageHost host = SignInPageHost.Start();
            using HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient(host.BaseAddress);
            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, "api-key");

            // Act
            InvalidAuthResponseException exception = await Assert.ThrowsExceptionAsync<InvalidAuthResponseException>(
                () => client.GetAsync("api/test"));

            // Assert — the sign-in page, not the configured base address, is what has to be reported: the base
            // address is the value the caller already believes, and believing it is the mistake being diagnosed.
            Assert.AreEqual($"{host.BaseAddress}{SignInPageHost.SignInPath}", exception.RequestUri?.ToString());
            Assert.AreEqual(HttpStatusCode.OK, exception.StatusCode);
            StringAssert.Contains(exception.ContentType, "text/html");
            StringAssert.Contains(exception.BodySnippet, "Sign in to continue");
        }
    }
}
