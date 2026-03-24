using System.Net;

namespace EasyReasy.Auth.Client.Tests
{
    [TestClass]
    public class AuthorizedHttpClientTests
    {
        private static AuthResponse CreateValidAuthResponse(string token = "test-token", int expiresInMinutes = 60, string? refreshToken = "test-refresh")
        {
            string expiresAt = DateTime.UtcNow.AddMinutes(expiresInMinutes).ToString("O");
            return new AuthResponse(token, expiresAt, refreshToken);
        }

        private static string CreateAuthResponseJson(string token = "test-token", int expiresInMinutes = 60, string? refreshToken = "test-refresh")
        {
            return CreateValidAuthResponse(token, expiresInMinutes, refreshToken).ToJson();
        }

        #region PreAuthorized Constructor

        [TestMethod]
        public void Constructor_PreAuthorized_SetsAuthenticationType()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };
            AuthResponse authResponse = CreateValidAuthResponse();

            // Act
            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, authResponse);

            // Assert
            Assert.AreEqual(AuthorizedHttpClient.AuthType.PreAuthorized, client.AuthenticationType);
        }

        [TestMethod]
        public void Constructor_PreAuthorized_NullHttpClient_ThrowsArgumentNullException()
        {
            // Arrange
            AuthResponse authResponse = CreateValidAuthResponse();

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new AuthorizedHttpClient(null!, authResponse));
        }

        [TestMethod]
        public void Constructor_PreAuthorized_NullAuthResponse_ThrowsArgumentNullException()
        {
            // Arrange
            HttpClient httpClient = new HttpClient() { BaseAddress = new Uri("https://example.com") };

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new AuthorizedHttpClient(httpClient, (AuthResponse)null!));
        }

        [TestMethod]
        public void Constructor_PreAuthorized_SetsAuthorizationHeader()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };
            AuthResponse authResponse = CreateValidAuthResponse(token: "my-jwt-token");

            // Act
            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, authResponse);

            // Assert
            Assert.AreEqual("Bearer", httpClient.DefaultRequestHeaders.Authorization?.Scheme);
            Assert.AreEqual("my-jwt-token", httpClient.DefaultRequestHeaders.Authorization?.Parameter);
        }

        [TestMethod]
        public void Constructor_PreAuthorized_AppendsTrailingSlashToBaseAddress()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };
            AuthResponse authResponse = CreateValidAuthResponse();

            // Act
            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, authResponse);

            // Assert
            Assert.AreEqual("https://example.com/", httpClient.BaseAddress?.ToString());
        }

        #endregion

        #region PreAuthorized — Requests use existing token

        [TestMethod]
        public async Task GetAsync_PreAuthorized_WithValidToken_DoesNotCallAuthEndpoint()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueJsonResponse("{\"data\": \"hello\"}");

            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            AuthResponse authResponse = CreateValidAuthResponse();

            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, authResponse);

            // Act
            HttpResponseMessage response = await client.GetAsync("api/test");

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, handler.SentRequests.Count);
            Assert.AreEqual("https://example.com/api/test", handler.SentRequests[0].RequestUri?.ToString());
        }

        [TestMethod]
        public async Task PostAsync_PreAuthorized_WithValidToken_SendsRequestWithBearerToken()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueJsonResponse("{\"ok\": true}");

            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            AuthResponse authResponse = CreateValidAuthResponse(token: "my-token");

            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, authResponse);
            StringContent content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

            // Act
            HttpResponseMessage response = await client.PostAsync("api/upload", content);

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("Bearer", handler.SentRequests[0].Headers.Authorization?.Scheme);
            Assert.AreEqual("my-token", handler.SentRequests[0].Headers.Authorization?.Parameter);
        }

        #endregion

        #region PreAuthorized — Token refresh

        [TestMethod]
        public async Task GetAsync_PreAuthorized_WithExpiredToken_RefreshesUsingRefreshToken()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();

            // First: refresh endpoint returns new token
            handler.EnqueueJsonResponse(CreateAuthResponseJson(token: "new-token", refreshToken: "new-refresh"));

            // Second: the actual GET request succeeds
            handler.EnqueueJsonResponse("{\"data\": \"hello\"}");

            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            AuthResponse authResponse = CreateValidAuthResponse(token: "expired-token", expiresInMinutes: -1, refreshToken: "old-refresh");

            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, authResponse);

            // Act
            HttpResponseMessage response = await client.GetAsync("api/test");

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(2, handler.SentRequests.Count);

            // First request should be to refresh endpoint
            Assert.IsTrue(handler.SentRequests[0].RequestUri?.ToString().Contains("api/auth/refresh"));

            // Second request should be the actual GET
            Assert.AreEqual("https://example.com/api/test", handler.SentRequests[1].RequestUri?.ToString());
        }

        [TestMethod]
        public async Task GetAsync_PreAuthorized_ExpiredTokenNoRefreshToken_ThrowsInvalidOperationException()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            AuthResponse authResponse = CreateValidAuthResponse(token: "expired-token", expiresInMinutes: -1, refreshToken: null);

            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, authResponse);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => client.GetAsync("api/test"));
        }

        [TestMethod]
        public async Task GetAsync_PreAuthorized_RefreshFails_ThrowsInvalidOperationException()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();

            // Refresh endpoint returns 401
            handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("Refresh token expired")
            });

            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            AuthResponse authResponse = CreateValidAuthResponse(token: "expired-token", expiresInMinutes: -1, refreshToken: "bad-refresh");

            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, authResponse);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => client.GetAsync("api/test"));
        }

        #endregion

        #region PreAuthorized — 401 retry with refresh

        [TestMethod]
        public async Task GetAsync_PreAuthorized_ServerReturns401_RefreshesAndRetries()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();

            // First: the GET returns 401
            handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.Unauthorized));

            // Second: refresh succeeds
            handler.EnqueueJsonResponse(CreateAuthResponseJson(token: "new-token", refreshToken: "new-refresh"));

            // Third: the retried GET succeeds
            handler.EnqueueJsonResponse("{\"data\": \"hello\"}");

            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            AuthResponse authResponse = CreateValidAuthResponse(token: "valid-token");

            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, authResponse);

            // Act
            HttpResponseMessage response = await client.GetAsync("api/test");

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(3, handler.SentRequests.Count);
        }

        #endregion

        #region UsernamePassword Constructor (existing behavior)

        [TestMethod]
        public async Task GetAsync_UsernamePassword_AuthenticatesBeforeRequest()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();

            // First: login endpoint returns token
            handler.EnqueueJsonResponse(CreateAuthResponseJson(token: "login-token"));

            // Second: the actual GET succeeds
            handler.EnqueueJsonResponse("{\"data\": \"hello\"}");

            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };

            using AuthorizedHttpClient client = new AuthorizedHttpClient(
                httpClient, username: "user", password: "pass");

            // Act
            HttpResponseMessage response = await client.GetAsync("api/test");

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(2, handler.SentRequests.Count);
            Assert.IsTrue(handler.SentRequests[0].RequestUri?.ToString().Contains("api/auth/login"));
        }

        [TestMethod]
        public async Task GetAsync_UsernamePassword_InvalidCredentials_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("Invalid credentials")
            });

            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };

            using AuthorizedHttpClient client = new AuthorizedHttpClient(
                httpClient, username: "user", password: "wrong-pass");

            // Act & Assert
            await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(
                () => client.GetAsync("api/test"));
        }

        #endregion

        #region ApiKey Constructor (existing behavior)

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthenticatesBeforeRequest()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();

            // First: apikey endpoint returns token
            handler.EnqueueJsonResponse(CreateAuthResponseJson(token: "apikey-token"));

            // Second: the actual GET succeeds
            handler.EnqueueJsonResponse("{\"data\": \"hello\"}");

            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };

            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, apiKey: "my-api-key");

            // Act
            HttpResponseMessage response = await client.GetAsync("api/test");

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(2, handler.SentRequests.Count);
            Assert.IsTrue(handler.SentRequests[0].RequestUri?.ToString().Contains("api/auth/apikey"));
        }

        #endregion

        #region EnsureAuthorizedAsync

        [TestMethod]
        public async Task EnsureAuthorizedAsync_PreAuthorized_ValidToken_DoesNothing()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            AuthResponse authResponse = CreateValidAuthResponse();

            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, authResponse);

            // Act
            await client.EnsureAuthorizedAsync();

            // Assert — no HTTP requests should have been made
            Assert.AreEqual(0, handler.SentRequests.Count);
        }

        #endregion
    }
}
