namespace EasyReasy.Auth.Client.Tests
{
    [TestClass]
    public class RemoteIntegrationTests
    {
        [TestMethod]
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
