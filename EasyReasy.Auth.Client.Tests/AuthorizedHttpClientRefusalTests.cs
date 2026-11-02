using System.Net;
using static EasyReasy.Auth.Client.Tests.ClientTestFactory;

namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// How the client reports a session it cannot renew, a refresh the server could not answer, and a sign-in the
    /// server refused, so that a caller can tell each apart by type and status rather than by message.
    /// </summary>
    [TestClass]
    public class AuthorizedHttpClientRefusalTests
    {
        #region Session not renewable

        [DataTestMethod]
        [DataRow(HttpStatusCode.BadRequest)]
        [DataRow(HttpStatusCode.Unauthorized)]
        [DataRow(HttpStatusCode.Forbidden)]
        [DataRow(HttpStatusCode.NotFound)]
        public async Task GetAsync_PreAuthorized_RefreshRefused_ThrowsSessionNotRenewableWithRefusalAsInner(HttpStatusCode refusal)
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(CreateStatusResponse(refusal));
            using AuthorizedHttpClient client = CreateExpiredPreAuthorizedClient(handler, "refused-refresh");

            // Act
            SessionNotRenewableException exception = await Assert.ThrowsExceptionAsync<SessionNotRenewableException>(() => client.GetAsync("api/test"));

            // Assert
            HttpRequestException? inner = exception.InnerException as HttpRequestException;
            Assert.IsNotNull(inner);
            Assert.AreEqual(refusal, inner.StatusCode);
        }

        [DataTestMethod]
        [DataRow(HttpStatusCode.BadRequest)]
        [DataRow(HttpStatusCode.Unauthorized)]
        [DataRow(HttpStatusCode.Forbidden)]
        [DataRow(HttpStatusCode.NotFound)]
        public async Task GetAsync_PreAuthorized_RefreshRefused_NextCallDoesNotRetryTheRefusedToken(HttpStatusCode refusal)
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(CreateStatusResponse(refusal));
            using AuthorizedHttpClient client = CreateExpiredPreAuthorizedClient(handler, "refused-refresh");
            await Assert.ThrowsExceptionAsync<SessionNotRenewableException>(() => client.GetAsync("api/test"));

            // Act
            SessionNotRenewableException exception = await Assert.ThrowsExceptionAsync<SessionNotRenewableException>(() => client.GetAsync("api/test"));

            // Assert — the refused token is gone, so the next call gives up without asking the server again.
            Assert.AreEqual(1, handler.SentRequests.Count);
            Assert.IsNull(exception.InnerException);
        }

        [TestMethod]
        public void Constructors_AllOverloads_PassMessageAndInnerExceptionToBase()
        {
            // Arrange — public, so a consumer's fake can throw one; the test project has no InternalsVisibleTo.
            InvalidOperationException inner = new InvalidOperationException("inner");

            // Act
            SessionNotRenewableException withoutArguments = new SessionNotRenewableException();
            SessionNotRenewableException withMessage = new SessionNotRenewableException("spent");
            SessionNotRenewableException withInner = new SessionNotRenewableException("spent", inner);

            // Assert
            Assert.IsNull(withoutArguments.InnerException);
            Assert.AreEqual("spent", withMessage.Message);
            Assert.AreEqual("spent", withInner.Message);
            Assert.AreSame(inner, withInner.InnerException);
        }

        #endregion

        #region Transient refresh failure

        [DataTestMethod]
        [DataRow(HttpStatusCode.RequestTimeout)]
        [DataRow(HttpStatusCode.TooManyRequests)]
        [DataRow(HttpStatusCode.InternalServerError)]
        [DataRow(HttpStatusCode.ServiceUnavailable)]
        [DataRow((HttpStatusCode)599)]
        public async Task GetAsync_PreAuthorized_RefreshTransient_ThrowsWithStatus(HttpStatusCode transientStatus)
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(CreateStatusResponse(transientStatus));
            using AuthorizedHttpClient client = CreateExpiredPreAuthorizedClient(handler, "kept-refresh");

            // Act
            HttpRequestException exception = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => client.GetAsync("api/test"));

            // Assert
            Assert.AreEqual(transientStatus, exception.StatusCode);
            StringAssert.StartsWith(exception.Message, $"Token refresh failed. Status: {transientStatus}, Content: ");
        }

        [DataTestMethod]
        [DataRow(HttpStatusCode.RequestTimeout)]
        [DataRow(HttpStatusCode.TooManyRequests)]
        [DataRow(HttpStatusCode.InternalServerError)]
        [DataRow(HttpStatusCode.ServiceUnavailable)]
        [DataRow((HttpStatusCode)599)]
        public async Task GetAsync_PreAuthorized_RefreshTransient_NextCallRedeemsSameRefreshToken(HttpStatusCode transientStatus)
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(CreateStatusResponse(transientStatus));
            handler.EnqueueJsonResponse(CreateAuthResponse("new-token", 60, "new-refresh").ToJson());
            handler.EnqueueJsonResponse("{\"data\": \"hello\"}");
            using AuthorizedHttpClient client = CreateExpiredPreAuthorizedClient(handler, "kept-refresh");
            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => client.GetAsync("api/test"));

            // Act
            HttpResponseMessage retried = await client.GetAsync("api/test");

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, retried.StatusCode);
            Assert.AreEqual(3, handler.SentRequests.Count);
            Assert.AreEqual(RefreshUri, handler.SentRequests[1].RequestUri?.ToString());
            string retriedRefreshBody = await handler.SentRequests[1].Content!.ReadAsStringAsync();
            StringAssert.Contains(retriedRefreshBody, "kept-refresh");
            Assert.AreEqual("new-token", handler.SentRequests[2].Headers.Authorization?.Parameter);
        }

        [TestMethod]
        public async Task GetAsync_PreAuthorized_RefreshUnreachable_ThrowsWithoutStatusAndKeepsRefreshToken()
        {
            // Arrange — the offline-laptop case: a spent-session report here would have the app delete a good session.
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueFailure(new HttpRequestException("Connection refused"));
            handler.EnqueueJsonResponse(CreateAuthResponse("new-token", 60, "new-refresh").ToJson());
            handler.EnqueueJsonResponse("{\"data\": \"hello\"}");
            using AuthorizedHttpClient client = CreateExpiredPreAuthorizedClient(handler, "kept-refresh");

            // Act
            HttpRequestException exception = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => client.GetAsync("api/test"));
            HttpResponseMessage retried = await client.GetAsync("api/test");

            // Assert
            Assert.IsNull(exception.StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, retried.StatusCode);
            string retriedRefreshBody = await handler.SentRequests[1].Content!.ReadAsStringAsync();
            StringAssert.Contains(retriedRefreshBody, "kept-refresh");
        }

        [DataTestMethod]
        [DataRow(HttpStatusCode.ServiceUnavailable)]
        [DataRow(HttpStatusCode.TooManyRequests)]
        [DataRow(HttpStatusCode.Unauthorized)]
        [DataRow(HttpStatusCode.Forbidden)]
        public async Task GetAsync_ApiKey_RefreshUnsuccessful_FallsBackToSigningIn(HttpStatusCode refreshStatus)
        {
            // Arrange — sign in once to hold a refresh token, then let that token expire.
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueJsonResponse(CreateAuthResponse("first-token", -1, "first-refresh").ToJson());
            handler.EnqueueJsonResponse("{\"data\": \"first\"}");
            handler.EnqueueResponse(CreateStatusResponse(refreshStatus));
            handler.EnqueueJsonResponse(CreateAuthResponse("signed-in-token", 60, "second-refresh").ToJson());
            handler.EnqueueJsonResponse("{\"data\": \"second\"}");
            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), "api-key");
            await client.GetAsync("api/test");

            // Act
            HttpResponseMessage response = await client.GetAsync("api/test");

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(5, handler.SentRequests.Count);
            Assert.AreEqual(RefreshUri, handler.SentRequests[2].RequestUri?.ToString());
            Assert.AreEqual(ApiKeySignInUri, handler.SentRequests[3].RequestUri?.ToString());
            Assert.AreEqual("signed-in-token", handler.SentRequests[4].Headers.Authorization?.Parameter);
        }

        #endregion

        #region Sign-in failure

        [DataTestMethod]
        [DataRow(HttpStatusCode.BadRequest)]
        [DataRow(HttpStatusCode.Forbidden)]
        [DataRow(HttpStatusCode.TooManyRequests)]
        [DataRow(HttpStatusCode.InternalServerError)]
        public async Task GetAsync_UsernamePassword_SignInUnsuccessful_ThrowsHttpRequestExceptionWithStatus(HttpStatusCode status)
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(CreateStatusResponse(status));
            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), username: "user", password: "pass");

            // Act
            HttpRequestException exception = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => client.GetAsync("api/test"));

            // Assert
            Assert.AreEqual(status, exception.StatusCode);
            StringAssert.StartsWith(exception.Message, $"Authentication failed. Status: {status}, Content: ");
        }

        [TestMethod]
        public async Task GetAsync_UsernamePassword_SignInAnswered401_StillThrowsUnauthorizedAccessException()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(CreateStatusResponse(HttpStatusCode.Unauthorized));
            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), username: "user", password: "wrong");

            // Act & Assert
            await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() => client.GetAsync("api/test"));
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_ServerUnreachable_ThrowsHttpRequestExceptionWithoutStatus()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueFailure(new HttpRequestException("Connection refused"));
            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), "api-key");

            // Act
            HttpRequestException exception = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => client.GetAsync("api/test"));

            // Assert
            Assert.IsNull(exception.StatusCode);
        }

        #endregion
    }
}
