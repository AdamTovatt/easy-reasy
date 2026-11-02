using System.Net;
using static EasyReasy.Auth.Client.Tests.ClientTestFactory;

namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// How calls waiting on the authorization lock behave when the attempt they waited behind finishes: they share
    /// its failure instead of each sending the same request in turn, while a call made after it finished tries
    /// again.
    /// </summary>
    /// <remarks>
    /// Each test holds one attempt in flight with a <see cref="HandlerGate"/> and only then starts the other calls.
    /// A call runs synchronously up to the lock it cannot take, so by the time its task is returned it has
    /// already queued — no sleeps are needed to order them. Every test carries a timeout so a deadlock fails
    /// instead of hanging the run.
    /// </remarks>
    [TestClass]
    public class AuthorizedHttpClientConcurrencyTests
    {
        private const int WaiterCount = 3;
        private const int TestTimeoutMilliseconds = 10000;

        private static List<Task<HttpResponseMessage>> StartGetCalls(AuthorizedHttpClient client, int count)
        {
            List<Task<HttpResponseMessage>> calls = new List<Task<HttpResponseMessage>>();
            for (int index = 0; index < count; index++)
            {
                calls.Add(client.GetAsync("api/test"));
            }

            return calls;
        }

        /// <summary>
        /// Makes one refresh fail with <c>503</c> and lets the call see it, so the client holds a reported failure.
        /// </summary>
        private static async Task FailOneRefreshAsync(FakeHttpHandler handler, AuthorizedHttpClient client)
        {
            handler.EnqueueResponse(CreateStatusResponse(HttpStatusCode.ServiceUnavailable));
            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => client.GetAsync("api/test"));
        }

        private static async Task<Exception?> CaptureFailureAsync(Task call)
        {
            try
            {
                await call;
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private static async Task AssertAllShareAsync(Exception expected, IEnumerable<Task<HttpResponseMessage>> waiters)
        {
            foreach (Task<HttpResponseMessage> waiter in waiters)
            {
                Assert.AreSame(expected, await CaptureFailureAsync(waiter));
            }
        }

        private static async Task AssertAllOkAsync(IEnumerable<Task<HttpResponseMessage>> calls)
        {
            foreach (Task<HttpResponseMessage> call in calls)
            {
                HttpResponseMessage response = await call;
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        [DataRow(HttpStatusCode.ServiceUnavailable, typeof(HttpRequestException))]
        [DataRow(HttpStatusCode.Unauthorized, typeof(SessionNotRenewableException))]
        public async Task GetAsync_PreAuthorized_RefreshFailsWithWaiters_WaitersShareTheFailure(HttpStatusCode refreshStatus, Type expectedType)
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            HandlerGate gate = handler.EnqueueGatedResponse(CreateStatusResponse(refreshStatus));
            using AuthorizedHttpClient client = CreateExpiredPreAuthorizedClient(handler, "refresh");

            Task<HttpResponseMessage> first = client.GetAsync("api/test");
            await gate.Entered;
            List<Task<HttpResponseMessage>> waiters = StartGetCalls(client, WaiterCount);

            // Act
            gate.Release();
            Exception? firstFailure = await CaptureFailureAsync(first);

            // Assert
            Assert.IsNotNull(firstFailure);
            Assert.IsInstanceOfType(firstFailure, expectedType);
            await AssertAllShareAsync(firstFailure, waiters);
            Assert.AreEqual(1, handler.SentRequests.Count);
        }

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task GetAsync_ApiKey_SignInFailsWithWaiters_WaitersShareTheFailure()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            HandlerGate gate = handler.EnqueueGatedResponse(CreateStatusResponse(HttpStatusCode.TooManyRequests));
            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), "api-key");

            Task<HttpResponseMessage> first = client.GetAsync("api/test");
            await gate.Entered;
            List<Task<HttpResponseMessage>> waiters = StartGetCalls(client, WaiterCount);

            // Act
            gate.Release();
            HttpRequestException firstFailure = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => first);

            // Assert
            await AssertAllShareAsync(firstFailure, waiters);
            Assert.AreEqual(1, handler.SentRequests.Count);
        }

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task GetAsync_PreAuthorized_HttpClientTimeoutWithWaiters_WaitersShareIt()
        {
            // Arrange — a timeout surfaces as a cancellation the caller did not ask for, and is shared like any failure.
            FakeHttpHandler handler = new FakeHttpHandler();
            HandlerGate gate = handler.EnqueueGatedFailure(new TaskCanceledException("The request timed out."));
            using AuthorizedHttpClient client = CreateExpiredPreAuthorizedClient(handler, "refresh");

            Task<HttpResponseMessage> first = client.GetAsync("api/test");
            await gate.Entered;
            List<Task<HttpResponseMessage>> waiters = StartGetCalls(client, WaiterCount);

            // Act
            gate.Release();
            TaskCanceledException firstFailure = await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => first);

            // Assert
            await AssertAllShareAsync(firstFailure, waiters);
            Assert.AreEqual(1, handler.SentRequests.Count);
        }

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task GetAsync_PreAuthorized_CallAfterFailedAttemptFinished_TriesAgain()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            using AuthorizedHttpClient client = CreateExpiredPreAuthorizedClient(handler, "refresh");
            await FailOneRefreshAsync(handler, client);
            handler.EnqueueJsonResponse(CreateAuthResponseJson("new-token", 60, "new-refresh"));
            handler.EnqueueJsonResponse("{\"data\": \"hello\"}");

            // Act
            HttpResponseMessage later = await client.GetAsync("api/test");

            // Assert — the failed refresh, the retried refresh, then the request itself.
            Assert.AreEqual(HttpStatusCode.OK, later.StatusCode);
            Assert.AreEqual(3, handler.SentRequests.Count);
            Assert.AreEqual(RefreshUri, handler.SentRequests[1].RequestUri?.ToString());
        }

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task GetAsync_PreAuthorized_SuccessAfterReportedFailure_WaitersDoNotGetTheOldFailure()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            using AuthorizedHttpClient client = CreateExpiredPreAuthorizedClient(handler, "refresh");
            await FailOneRefreshAsync(handler, client);
            HandlerGate gate = handler.EnqueueGatedJsonResponse(CreateAuthResponseJson("new-token", 60, "new-refresh"));
            for (int index = 0; index < WaiterCount + 1; index++)
            {
                handler.EnqueueJsonResponse("{\"data\": \"hello\"}");
            }

            Task<HttpResponseMessage> first = client.GetAsync("api/test");
            await gate.Entered;
            List<Task<HttpResponseMessage>> waiters = StartGetCalls(client, WaiterCount);

            // Act
            gate.Release();

            // Assert
            await AssertAllOkAsync(waiters.Prepend(first));
        }

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task ForceAuthorizeAsync_WaitingBehindSuccessAfterReportedFailure_DoesNotGetTheOldFailure()
        {
            // Arrange — a forced authorize takes the finished attempt's outcome whether or not a token is still
            // needed, so it is the waiter that would be handed a failure left over from before the success.
            FakeHttpHandler handler = new FakeHttpHandler();
            using AuthorizedHttpClient client = CreateExpiredPreAuthorizedClient(handler, "refresh");
            await FailOneRefreshAsync(handler, client);
            HandlerGate gate = handler.EnqueueGatedJsonResponse(CreateAuthResponseJson("new-token", 60, "new-refresh"));
            handler.EnqueueJsonResponse("{\"data\": \"hello\"}");

            Task<HttpResponseMessage> first = client.GetAsync("api/test");
            await gate.Entered;
            Task forced = client.ForceAuthorizeAsync();

            // Act
            gate.Release();
            await AssertAllOkAsync(new[] { first });

            // Assert — completes without throwing.
            await forced;
        }

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task ForceAuthorizeAsync_WaitingBehindCancelledAttempt_MakesItsOwnAttempt()
        {
            // Arrange — a cancelled attempt renewed nothing, so taking it as a success would return without a token.
            FakeHttpHandler handler = new FakeHttpHandler();
            HandlerGate gate = handler.EnqueueGatedResponse(CreateStatusResponse(HttpStatusCode.ServiceUnavailable));
            handler.EnqueueJsonResponse(CreateAuthResponseJson("signed-in-token", 60, "refresh"));
            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), "api-key");
            using CancellationTokenSource firstCallerCancellation = new CancellationTokenSource();

            Task<HttpResponseMessage> first = client.GetAsync("api/test", firstCallerCancellation.Token);
            await gate.Entered;
            Task forced = client.ForceAuthorizeAsync();

            // Act
            firstCallerCancellation.Cancel();
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => first);
            await forced;

            // Assert — the cancelled sign-in, then the forced call's own.
            Assert.AreEqual(2, handler.SentRequests.Count);
            Assert.AreEqual(ApiKeySignInUri, handler.SentRequests[1].RequestUri?.ToString());
        }

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task ForceReauthorizeAsync_FailsWithWaiters_WaitersShareTheFailure()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            HandlerGate gate = handler.EnqueueGatedResponse(CreateStatusResponse(HttpStatusCode.ServiceUnavailable));
            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), "api-key");

            Task reauthorized = client.ForceReauthorizeAsync();
            await gate.Entered;
            List<Task<HttpResponseMessage>> waiters = StartGetCalls(client, WaiterCount);

            // Act
            gate.Release();
            HttpRequestException failure = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => reauthorized);

            // Assert
            await AssertAllShareAsync(failure, waiters);
            Assert.AreEqual(1, handler.SentRequests.Count);
        }

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task GetAsync_ApiKey_SignInSucceedsWithWaiters_WaitersUseTheNewToken()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            HandlerGate gate = handler.EnqueueGatedJsonResponse(CreateAuthResponseJson("signed-in-token", 60, "refresh"));
            for (int index = 0; index < WaiterCount + 1; index++)
            {
                handler.EnqueueJsonResponse("{\"data\": \"hello\"}");
            }

            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), "api-key");

            Task<HttpResponseMessage> first = client.GetAsync("api/test");
            await gate.Entered;
            List<Task<HttpResponseMessage>> waiters = StartGetCalls(client, WaiterCount);

            // Act
            gate.Release();
            await AssertAllOkAsync(waiters.Prepend(first));

            // Assert — one sign-in, then one request per caller.
            Assert.AreEqual(WaiterCount + 2, handler.SentRequests.Count);
            Assert.AreEqual(1, handler.SentRequests.Count(request => request.RequestUri?.ToString() == ApiKeySignInUri));
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        [DataRow(false)]
        [DataRow(true)]
        public async Task GetAsync_PreAuthorized_FirstCallerCancelled_WaiterMakesItsOwnAttempt(bool afterReportedFailure)
        {
            // Arrange — one caller's cancellation says nothing about whether another's attempt would succeed. With
            // an earlier failure already reported, the waiter must not be handed that one either.
            FakeHttpHandler handler = new FakeHttpHandler();
            using AuthorizedHttpClient client = CreateExpiredPreAuthorizedClient(handler, "refresh");
            if (afterReportedFailure)
            {
                await FailOneRefreshAsync(handler, client);
            }

            HandlerGate gate = handler.EnqueueGatedResponse(CreateStatusResponse(HttpStatusCode.ServiceUnavailable));
            handler.EnqueueJsonResponse(CreateAuthResponseJson("new-token", 60, "new-refresh"));
            handler.EnqueueJsonResponse("{\"data\": \"hello\"}");
            using CancellationTokenSource firstCallerCancellation = new CancellationTokenSource();

            Task<HttpResponseMessage> first = client.GetAsync("api/test", firstCallerCancellation.Token);
            await gate.Entered;
            Task<HttpResponseMessage> waiter = client.GetAsync("api/test");

            // Act
            firstCallerCancellation.Cancel();
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => first);
            HttpResponseMessage waiterResponse = await waiter;

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, waiterResponse.StatusCode);
        }

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task ForceAuthorizeAsync_WaitingBehindFailedSignIn_SharesTheFailure()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            HandlerGate gate = handler.EnqueueGatedResponse(CreateStatusResponse(HttpStatusCode.ServiceUnavailable));
            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), "api-key");

            Task<HttpResponseMessage> first = client.GetAsync("api/test");
            await gate.Entered;
            Task forced = client.ForceAuthorizeAsync();

            // Act
            gate.Release();
            HttpRequestException firstFailure = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => first);
            HttpRequestException forcedFailure = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => forced);

            // Assert
            Assert.AreSame(firstFailure, forcedFailure);
            Assert.AreEqual(1, handler.SentRequests.Count);
        }

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task ForceAuthorizeAsync_WaitingBehindSuccessfulSignIn_DoesNotSignInAgain()
        {
            // Arrange — as when several requests answered 401 at once: the first renewal serves them all.
            FakeHttpHandler handler = new FakeHttpHandler();
            HandlerGate gate = handler.EnqueueGatedJsonResponse(CreateAuthResponseJson("signed-in-token", 60, "refresh"));
            handler.EnqueueJsonResponse("{\"data\": \"hello\"}");
            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), "api-key");

            Task<HttpResponseMessage> first = client.GetAsync("api/test");
            await gate.Entered;
            Task forced = client.ForceAuthorizeAsync();

            // Act
            gate.Release();
            await AssertAllOkAsync(new[] { first });
            await forced;

            // Assert — the sign-in and the request, and nothing for the forced call.
            Assert.AreEqual(2, handler.SentRequests.Count);
        }

        [TestMethod]
        [Timeout(TestTimeoutMilliseconds)]
        public async Task ForceReauthorizeAsync_WaitingBehindFailedSignIn_MakesItsOwnAttempt()
        {
            // Arrange — an explicit reauthorize always clears and tries afresh.
            FakeHttpHandler handler = new FakeHttpHandler();
            HandlerGate gate = handler.EnqueueGatedResponse(CreateStatusResponse(HttpStatusCode.ServiceUnavailable));
            handler.EnqueueJsonResponse(CreateAuthResponseJson("signed-in-token", 60, "refresh"));
            using AuthorizedHttpClient client = new AuthorizedHttpClient(CreateHttpClient(handler), "api-key");

            Task<HttpResponseMessage> first = client.GetAsync("api/test");
            await gate.Entered;
            Task reauthorized = client.ForceReauthorizeAsync();

            // Act
            gate.Release();
            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => first);
            await reauthorized;

            // Assert — the failed sign-in, then the reauthorize's own.
            Assert.AreEqual(2, handler.SentRequests.Count);
            Assert.AreEqual(ApiKeySignInUri, handler.SentRequests[1].RequestUri?.ToString());
        }
    }
}
