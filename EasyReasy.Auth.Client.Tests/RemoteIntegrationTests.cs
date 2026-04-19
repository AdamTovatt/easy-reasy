namespace EasyReasy.Auth.Client.Tests
{
    [TestClass]
    public class RemoteIntegrationTests
    {
        [TestMethod]
        [Ignore("Local-only smoke test: fill in a real base address and API key, then remove [Ignore] to run against a live server.")]
        public async Task CreateAuthenticatedClient()
        {
            // Arrange
            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("url here");

            // Act
            AuthorizedHttpClient authorizedHttpClient = new AuthorizedHttpClient(httpClient, "api key here");
            await authorizedHttpClient.EnsureAuthorizedAsync();
        }
    }
}
