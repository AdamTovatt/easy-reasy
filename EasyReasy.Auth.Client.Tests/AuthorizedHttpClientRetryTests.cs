using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using static EasyReasy.Auth.Client.Tests.ClientTestFactory;

namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// What the send methods do when the server answers <c>401</c>: dispose that response, renew once, and send a
    /// copy of the request carrying what the caller set and the same body.
    /// </summary>
    [TestClass]
    public class AuthorizedHttpClientRetryTests
    {
        private const string OriginalToken = "valid-token";
        private const string RenewedToken = "new-token";

        /// <summary>
        /// Builds a pre-authorized client whose first request the server answers with <paramref name="firstResponse"/>,
        /// then renews through the refresh endpoint, then answers <paramref name="retryResponse"/>.
        /// </summary>
        private static AuthorizedHttpClient CreateClientAnswering401(
            FakeHttpHandler handler,
            HttpResponseMessage firstResponse,
            HttpResponseMessage retryResponse,
            HttpMessageHandler? clientHandler = null)
        {
            handler.EnqueueResponse(firstResponse);
            handler.EnqueueJsonResponse(CreateAuthResponseJson(token: RenewedToken, refreshToken: "new-refresh"));
            handler.EnqueueResponse(retryResponse);

            HttpClient httpClient = clientHandler == null
                ? CreateHttpClient(handler)
                : new HttpClient(clientHandler) { BaseAddress = new Uri("https://example.com/") };

            return new AuthorizedHttpClient(httpClient, CreateAuthResponse(token: OriginalToken));
        }

        private static AuthorizedHttpClient CreateClientAnswering401ThenOk(FakeHttpHandler handler)
        {
            return CreateClientAnswering401(
                handler, new HttpResponseMessage(HttpStatusCode.Unauthorized), new HttpResponseMessage(HttpStatusCode.OK));
        }

        [TestMethod]
        public async Task SendAsync_Get_ServerReturns401_RenewsAndReturnsTheRetriedResponse()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            using AuthorizedHttpClient client = CreateClientAnswering401ThenOk(handler);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "api/test");

            // Act
            HttpResponseMessage response = await client.SendAsync(request);

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(3, handler.SentRequests.Count);
            Assert.AreEqual(RefreshUri, handler.SentRequests[1].RequestUri?.ToString());

            HttpRequestMessage retried = handler.SentRequests[2];
            Assert.AreNotSame(request, retried);
            Assert.AreEqual(HttpMethod.Get, retried.Method);
            Assert.AreEqual("https://example.com/api/test", retried.RequestUri?.ToString());
        }

        [TestMethod]
        public async Task SendAsync_PostWithJsonBody_ServerReturns401_ResendsTheSameBodyAndContentType()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            using AuthorizedHttpClient client = CreateClientAnswering401ThenOk(handler);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/items")
            {
                Content = JsonContent.Create(new { name = "widget", count = 3 }),
            };

            // Act
            HttpResponseMessage response = await client.SendAsync(request);

            // Assert
            IReadOnlyList<SentRequest> sent = handler.Sent;
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("{\"name\":\"widget\",\"count\":3}", sent[0].Body);
            Assert.AreEqual(sent[0].Body, sent[2].Body);
            Assert.AreEqual(HttpMethod.Post, sent[2].Message.Method);
            Assert.AreEqual("application/json", sent[2].Message.Content?.Headers.ContentType?.MediaType);
            Assert.AreEqual(
                sent[0].Message.Content?.Headers.ContentType?.ToString(),
                sent[2].Message.Content?.Headers.ContentType?.ToString());
        }

        [TestMethod]
        public async Task SendAsync_ServerReturns401_RetriedRequestCarriesCallerHeadersOptionsAndVersion()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            using AuthorizedHttpClient client = CreateClientAnswering401ThenOk(handler);
            HttpRequestOptionsKey<string> optionKey = new HttpRequestOptionsKey<string>("trace-id");
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "api/test")
            {
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            };
            request.Headers.Add("X-Custom", new[] { "first", "second" });
            request.Headers.Accept.ParseAdd("application/xml");
            request.Options.Set(optionKey, "abc123");

            // Act
            await client.SendAsync(request);

            // Assert
            HttpRequestMessage retried = handler.SentRequests[2];
            CollectionAssert.AreEqual(new[] { "first", "second" }, retried.Headers.GetValues("X-Custom").ToArray());
            Assert.AreEqual("application/xml", retried.Headers.Accept.Single().MediaType);
            Assert.IsTrue(retried.Options.TryGetValue(optionKey, out string? optionValue));
            Assert.AreEqual("abc123", optionValue);
            Assert.AreEqual(HttpVersion.Version20, retried.Version);
            Assert.AreEqual(HttpVersionPolicy.RequestVersionExact, retried.VersionPolicy);
        }

        [TestMethod]
        public async Task SendAsync_HandlerWritesAnOptionWhileSending_ServerReturns401_RetriedRequestDoesNotCarryIt()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            OptionStampingHandler stampingHandler = new OptionStampingHandler(handler);
            using AuthorizedHttpClient client = CreateClientAnswering401(
                handler,
                new HttpResponseMessage(HttpStatusCode.Unauthorized),
                new HttpResponseMessage(HttpStatusCode.OK),
                stampingHandler);

            // Act
            await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "api/test"));

            // Assert: the first send, the refresh and the retry each arrived without the handler's own state.
            CollectionAssert.AreEqual(new[] { false, false, false }, stampingHandler.ArrivedStamped.ToArray());
        }

        [TestMethod]
        public async Task SendAsync_ServerReturns401_RetriedRequestCarriesTheRenewedToken()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            using AuthorizedHttpClient client = CreateClientAnswering401ThenOk(handler);

            // Act
            await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "api/test"));

            // Assert
            Assert.AreEqual(OriginalToken, handler.SentRequests[0].Headers.Authorization?.Parameter);
            HttpRequestMessage retried = handler.SentRequests[2];
            Assert.AreEqual("Bearer", retried.Headers.Authorization?.Scheme);
            Assert.AreEqual(RenewedToken, retried.Headers.Authorization?.Parameter);
            Assert.AreEqual(1, retried.Headers.GetValues("Authorization").Count());
        }

        [TestMethod]
        public async Task SendAsync_CallerSetAuthorizationHeader_ServerReturns401_RetriedRequestCarriesItAsSet()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            using AuthorizedHttpClient client = CreateClientAnswering401ThenOk(handler);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "api/test");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", "callers-own");

            // Act
            await client.SendAsync(request);

            // Assert
            HttpRequestMessage retried = handler.SentRequests[2];
            Assert.AreEqual("Basic", retried.Headers.Authorization?.Scheme);
            Assert.AreEqual("callers-own", retried.Headers.Authorization?.Parameter);
        }

        [TestMethod]
        public async Task SendAsync_StreamContentOverSeekableStream_ServerReturns401_ResendsFromWhereTheStreamStarted()
        {
            // Arrange: the stream starts partway in, so only what follows is the body.
            FakeHttpHandler handler = new FakeHttpHandler();
            using AuthorizedHttpClient client = CreateClientAnswering401ThenOk(handler);
            MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes("skip|the rest of the upload"));
            stream.Position = 5;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/upload")
            {
                Content = new StreamContent(stream),
            };

            // Act
            HttpResponseMessage response = await client.SendAsync(request);

            // Assert
            IReadOnlyList<SentRequest> sent = handler.Sent;
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("the rest of the upload", sent[0].Body);
            Assert.AreEqual("the rest of the upload", sent[2].Body);
        }

        [TestMethod]
        public async Task SendAsync_StreamContentOverNonSeekableStream_ServerReturns401_ThrowsWithoutSendingABody()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            using AuthorizedHttpClient client = CreateClientAnswering401ThenOk(handler);
            const string body = "a body that can be read once";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/upload")
            {
                Content = new StreamContent(new NonSeekableStream(Encoding.UTF8.GetBytes(body))),
            };

            // Act
            HttpRequestException exception = await Assert.ThrowsExceptionAsync<HttpRequestException>(
                () => client.SendAsync(request));

            // Assert: the handler's wrapping of the consumed stream, and only the first send and the refresh went out.
            Assert.IsNull(exception.StatusCode);
            Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
            IReadOnlyList<SentRequest> sent = handler.Sent;
            Assert.AreEqual(2, sent.Count);
            Assert.AreEqual(body, sent[0].Body);
            Assert.AreEqual(RefreshUri, sent[1].Message.RequestUri?.ToString());
        }

        [TestMethod]
        public async Task SendAsync_ServerReturns401_DisposesTheFirstResponse()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            DisposeTrackingContent firstContent = new DisposeTrackingContent();
            using AuthorizedHttpClient client = CreateClientAnswering401(
                handler,
                new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = firstContent },
                new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "api/test"));

            // Assert
            Assert.IsTrue(firstContent.IsDisposed);
        }

        [TestMethod]
        public async Task GetAsync_ServerReturns401_DisposesTheFirstResponseAndReturnsTheRetriedResponse()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            DisposeTrackingContent firstContent = new DisposeTrackingContent();
            using AuthorizedHttpClient client = CreateClientAnswering401(
                handler,
                new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = firstContent },
                new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            HttpResponseMessage response = await client.GetAsync("api/test");

            // Assert
            Assert.IsTrue(firstContent.IsDisposed);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(3, handler.SentRequests.Count);
        }

        [TestMethod]
        public async Task PostAsync_ServerReturns401_DisposesTheFirstResponseAndResendsTheBody()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            DisposeTrackingContent firstContent = new DisposeTrackingContent();
            using AuthorizedHttpClient client = CreateClientAnswering401(
                handler,
                new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = firstContent },
                new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            HttpResponseMessage response = await client.PostAsync("api/items", new StringContent("posted"));

            // Assert
            Assert.IsTrue(firstContent.IsDisposed);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("posted", handler.Sent[2].Body);
        }

        [TestMethod]
        public async Task SendAsync_ServerReturns401AfterRenewal_ReturnsTheSecond401WithoutRetryingAgain()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            DisposeTrackingContent secondContent = new DisposeTrackingContent();
            HttpResponseMessage second401 = new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = secondContent };
            using AuthorizedHttpClient client = CreateClientAnswering401(
                handler, new HttpResponseMessage(HttpStatusCode.Unauthorized), second401);

            // Act
            HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "api/test"));

            // Assert
            Assert.AreSame(second401, response);
            Assert.IsFalse(secondContent.IsDisposed);
            Assert.AreEqual(3, handler.SentRequests.Count);
        }

        /// <summary>
        /// One status from each of the shared refusal and server-failure lists, with what a pre-authorized client
        /// throws when its refresh is answered that way.
        /// </summary>
        public static IEnumerable<object[]> FailedRenewals =>
        [
            [HttpStatusCode.Forbidden, typeof(SessionNotRenewableException)],
            [HttpStatusCode.ServiceUnavailable, typeof(HttpRequestException)],
        ];

        [DataTestMethod]
        [DynamicData(nameof(FailedRenewals))]
        public async Task SendAsync_ServerReturns401AndRenewalFails_ThrowsWithoutRetryingAndDisposesThe401(
            HttpStatusCode refreshStatus, Type expectedException)
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            DisposeTrackingContent firstContent = new DisposeTrackingContent();
            handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = firstContent });
            handler.EnqueueResponse(CreateStatusResponse(refreshStatus));
            using AuthorizedHttpClient client = new AuthorizedHttpClient(
                CreateHttpClient(handler), CreateAuthResponse(token: OriginalToken));

            // Act
            Exception? exception = null;
            try
            {
                await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "api/test"));
            }
            catch (Exception thrown)
            {
                exception = thrown;
            }

            // Assert
            Assert.AreEqual(expectedException, exception?.GetType());
            Assert.IsTrue(firstContent.IsDisposed);
            Assert.AreEqual(2, handler.SentRequests.Count);
        }

        [TestMethod]
        public async Task GetAsync_HttpClientDefaultsToHttp2_SendsWithTheDefaultVersionAndPolicy()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK));
            using AuthorizedHttpClient client = new AuthorizedHttpClient(
                CreateHttp2HttpClient(handler), CreateAuthResponse(token: OriginalToken));

            // Act
            await client.GetAsync("api/test");

            // Assert
            Assert.AreEqual(HttpVersion.Version20, handler.SentRequests[0].Version);
            Assert.AreEqual(HttpVersionPolicy.RequestVersionExact, handler.SentRequests[0].VersionPolicy);
        }

        [TestMethod]
        public async Task PostAsync_HttpClientDefaultsToHttp2_SendsWithTheDefaultVersionAndPolicy()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK));
            using AuthorizedHttpClient client = new AuthorizedHttpClient(
                CreateHttp2HttpClient(handler), CreateAuthResponse(token: OriginalToken));

            // Act
            await client.PostAsync("api/items", new StringContent("posted"));

            // Assert
            Assert.AreEqual(HttpVersion.Version20, handler.SentRequests[0].Version);
            Assert.AreEqual(HttpVersionPolicy.RequestVersionExact, handler.SentRequests[0].VersionPolicy);
        }

        private static HttpClient CreateHttp2HttpClient(FakeHttpHandler handler)
        {
            HttpClient httpClient = CreateHttpClient(handler);
            httpClient.DefaultRequestVersion = HttpVersion.Version20;
            httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            return httpClient;
        }
    }
}
