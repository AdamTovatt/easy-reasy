using System.Net;
using static EasyReasy.Auth.Client.Tests.ClientTestFactory;

namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// How the client reports a session it cannot renew, a refresh the server failed rather than refused, and a
    /// sign-in the server refused, so that a caller can tell each apart by type and status rather than by message.
    /// </summary>
    [TestClass]
    public class AuthorizedHttpClientRefusalTests
    {
        #region Session not renewable

        [DataTestMethod]
        [DynamicData(nameof(RefusalStatuses), typeof(ClientTestFactory))]
        public async Task GetAsync_PreAuthorized_RefreshRefused_ThrowsSessionNotRenewableWithRefusalAsInner(HttpStatusCode refusal)
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(CreateStatusResponse(refusal));
            using AuthorizedHttpClient client = CreateExpiredPreAuthorizedClient(handler, "refused-refresh");

            // Act
            SessionNotRenewableException exception = await Assert.ThrowsExceptionAsync<SessionNotRenewableException>(() => client.GetAsync("api/test"));

            // Assert — and part of the family a single catch (AuthClientException) handles.
            Assert.IsInstanceOfType(exception, typeof(AuthClientException));
            HttpRequestException? inner = exception.InnerException as HttpRequestException;
            Assert.IsNotNull(inner);
            Assert.AreEqual(refusal, inner.StatusCode);
        }

        [TestMethod]
        public async Task GetAsync_PreAuthorized_RefusedAndMissingRefreshToken_MessagesTellThemApart()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(CreateStatusResponse(HttpStatusCode.NotFound));
            using AuthorizedHttpClient refusedClient = CreateExpiredPreAuthorizedClient(handler, "refused-refresh");
            using AuthorizedHttpClient missingClient = CreateExpiredPreAuthorizedClient(new FakeHttpHandler(), refreshToken: null);

            // Act
            SessionNotRenewableException refused = await Assert.ThrowsExceptionAsync<SessionNotRenewableException>(() => refusedClient.GetAsync("api/test"));
            SessionNotRenewableException missing = await Assert.ThrowsExceptionAsync<SessionNotRenewableException>(() => missingClient.GetAsync("api/test"));

            // Assert — a log line says which it was, and a refusal names its status.
            Assert.AreNotEqual(missing.Message, refused.Message);
            StringAssert.Contains(refused.Message, "404 NotFound");
        }

        [DataTestMethod]
        [DynamicData(nameof(RefusalStatuses), typeof(ClientTestFactory))]
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
        public async Task ForceReauthorizeAsync_PreAuthorizedWithValidSession_ThrowsSessionNotRenewable()
        {
            // Arrange — reauthorize clears the refresh token, and such a client has no credentials to fall back on.
            FakeHttpHandler handler = new FakeHttpHandler();
            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), CreateAuthResponse("valid-token", 60, "good-refresh"));

            // Act
            await Assert.ThrowsExceptionAsync<SessionNotRenewableException>(() => client.ForceReauthorizeAsync());

            // Assert — nothing was sent: the session was spent locally.
            Assert.AreEqual(0, handler.SentRequests.Count);
        }

        [TestMethod]
        public async Task ForceAuthorizeAsync_PreAuthorizedWithValidSession_RedeemsTheRefreshToken()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueJsonResponse(CreateAuthResponseJson("renewed-token", 60, "new-refresh"));
            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), CreateAuthResponse("valid-token", 60, "good-refresh"));

            // Act
            await client.ForceAuthorizeAsync();

            // Assert
            Assert.AreEqual(1, handler.SentRequests.Count);
            Assert.AreEqual(RefreshUri, handler.SentRequests[0].RequestUri?.ToString());
            StringAssert.Contains(await handler.SentRequests[0].Content!.ReadAsStringAsync(), "good-refresh");
        }

        [TestMethod]
        public void SessionNotRenewableException_AllConstructors_PassMessageAndInnerExceptionToBase()
        {
            // Arrange — public, so a consumer's fake can throw one; the test project has no InternalsVisibleTo.
            HttpRequestException inner = new HttpRequestException("inner");

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

        #region Refresh server failure

        [DataTestMethod]
        [DynamicData(nameof(ServerFailureStatuses), typeof(ClientTestFactory))]
        public async Task GetAsync_PreAuthorized_RefreshServerFailed_ThrowsWithStatus(HttpStatusCode failureStatus)
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(CreateStatusResponse(failureStatus));
            using AuthorizedHttpClient client = CreateExpiredPreAuthorizedClient(handler, "kept-refresh");

            // Act
            HttpRequestException exception = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => client.GetAsync("api/test"));

            // Assert
            Assert.AreEqual(failureStatus, exception.StatusCode);
            StringAssert.Contains(exception.Message, $"{(int)failureStatus} {failureStatus}");
        }

        [DataTestMethod]
        [DynamicData(nameof(ServerFailureStatuses), typeof(ClientTestFactory))]
        public async Task GetAsync_PreAuthorized_RefreshServerFailed_NextCallRedeemsSameRefreshToken(HttpStatusCode failureStatus)
        {
            // Arrange — an unfollowed 3xx judged nothing about the token, so it is kept like a 5xx.
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(CreateStatusResponse(failureStatus));
            handler.EnqueueJsonResponse(CreateAuthResponseJson("new-token", 60, "new-refresh"));
            handler.EnqueueJsonResponse("{\"data\": \"hello\"}");
            using AuthorizedHttpClient client = CreateExpiredPreAuthorizedClient(handler, "kept-refresh");
            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => client.GetAsync("api/test"));

            // Act
            HttpResponseMessage retried = await client.GetAsync("api/test");

            // Assert — the failed refresh, the retried refresh, then the request itself.
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
            handler.EnqueueJsonResponse(CreateAuthResponseJson("new-token", 60, "new-refresh"));
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
            // Arrange — sign in once to hold a refresh token, then let that token expire. Failure and refusal rows
            // alike: a client with credentials signs in either way.
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueJsonResponse(CreateAuthResponseJson("first-token", -1, "first-refresh"));
            handler.EnqueueJsonResponse("{\"data\": \"first\"}");
            handler.EnqueueResponse(CreateStatusResponse(refreshStatus));
            handler.EnqueueJsonResponse(CreateAuthResponseJson("signed-in-token", 60, "second-refresh"));
            handler.EnqueueJsonResponse("{\"data\": \"second\"}");
            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), "api-key");
            await client.GetAsync("api/test");

            // Act
            HttpResponseMessage response = await client.GetAsync("api/test");

            // Assert — the first sign-in and request, then the unsuccessful refresh, the fallback sign-in and the
            // second request.
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(5, handler.SentRequests.Count);
            Assert.AreEqual(RefreshUri, handler.SentRequests[2].RequestUri?.ToString());
            Assert.AreEqual(ApiKeySignInUri, handler.SentRequests[3].RequestUri?.ToString());
            Assert.AreEqual("signed-in-token", handler.SentRequests[4].Headers.Authorization?.Parameter);
        }

        #endregion

        #region Sign-in failure

        [DataTestMethod]
        [DynamicData(nameof(RefusalStatuses), typeof(ClientTestFactory))]
        public async Task GetAsync_UsernamePassword_SignInRefused_ThrowsSignInRefusedExceptionWithStatus(HttpStatusCode status)
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(CreateStatusResponse(status));
            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), username: "user", password: "pass");

            // Act
            SignInRefusedException exception = await Assert.ThrowsExceptionAsync<SignInRefusedException>(() => client.GetAsync("api/test"));

            // Assert — and part of the family a single catch (AuthClientException) handles.
            Assert.AreEqual(status, exception.StatusCode);
            StringAssert.Contains(exception.Message, $"{(int)status} {status}");
            StringAssert.Contains(exception.Message, StatusResponseBody);
            Assert.IsInstanceOfType(exception, typeof(AuthClientException));
        }

        [DataTestMethod]
        [DynamicData(nameof(ServerFailureStatuses), typeof(ClientTestFactory))]
        public async Task GetAsync_UsernamePassword_SignInServerFails_ThrowsHttpRequestExceptionWithStatus(HttpStatusCode status)
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(CreateStatusResponse(status));
            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), username: "user", password: "pass");

            // Act
            HttpRequestException exception = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => client.GetAsync("api/test"));

            // Assert
            Assert.AreEqual(status, exception.StatusCode);
            StringAssert.Contains(exception.Message, $"{(int)status} {status}");
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

        [TestMethod]
        public void SignInRefusedException_AllConstructors_KeepStatusMessageAndInnerException()
        {
            // Arrange — public, so a consumer's fake can throw one.
            HttpRequestException inner = new HttpRequestException("inner");

            // Act
            SignInRefusedException withStatus = new SignInRefusedException(HttpStatusCode.Forbidden);
            SignInRefusedException withMessage = new SignInRefusedException("refused", HttpStatusCode.BadRequest);
            SignInRefusedException withInner = new SignInRefusedException("refused", HttpStatusCode.Unauthorized, inner);

            // Assert
            Assert.AreEqual(HttpStatusCode.Forbidden, withStatus.StatusCode);
            StringAssert.Contains(withStatus.Message, "403 Forbidden");
            Assert.AreEqual(HttpStatusCode.BadRequest, withMessage.StatusCode);
            Assert.AreEqual("refused", withMessage.Message);
            Assert.AreEqual(HttpStatusCode.Unauthorized, withInner.StatusCode);
            Assert.AreSame(inner, withInner.InnerException);
        }

        #endregion
    }
}
