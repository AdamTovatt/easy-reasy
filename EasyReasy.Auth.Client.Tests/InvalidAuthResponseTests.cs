using System.Net;
using System.Text.Json;

namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// Covers what an <see cref="AuthorizedHttpClient"/> reports when the address it is pointed at answers its auth
    /// or refresh path with something that is not an <see cref="AuthResponse"/> — the shape a host that is not an
    /// auth server produces — and what of that answer is allowed to reach a message.
    /// </summary>
    [TestClass]
    public class InvalidAuthResponseTests
    {
        /// <summary>
        /// Enqueues a <c>200</c> answering with HTML, the way a host that is not an auth server answers.
        /// </summary>
        /// <param name="handler">The handler to enqueue on.</param>
        /// <param name="html">The page to answer with. Named at every call site, since what is in it is what
        /// several of these tests are about.</param>
        private static void EnqueueHtmlResponse(FakeHttpHandler handler, string html)
        {
            handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html"),
            });
        }

        /// <summary>
        /// Points an API-key client at the handler and returns what its first request throws.
        /// </summary>
        /// <param name="handler">The handler holding what the auth endpoint answers with.</param>
        /// <returns>The exception the request failed with.</returns>
        private static async Task<InvalidAuthResponseException> CaptureApiKeyFailureAsync(FakeHttpHandler handler)
        {
            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, "api-key");

            return await Assert.ThrowsExceptionAsync<InvalidAuthResponseException>(() => client.GetAsync("api/test"));
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersHtml_ThrowsCarryingTheEndpointStatusContentTypeAndBody()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            EnqueueHtmlResponse(handler, SignInPageHost.SignInPageHtml);

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert
            Assert.AreEqual("https://example.com/api/auth/apikey", exception.RequestUri?.ToString());
            Assert.AreEqual(HttpStatusCode.OK, exception.StatusCode);
            Assert.AreEqual("text/html; charset=utf-8", exception.ContentType);
            StringAssert.Contains(exception.BodySnippet, "<!DOCTYPE html>");
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersHtml_MessageNamesTheEndpointStatusAndContentType()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            EnqueueHtmlResponse(handler, SignInPageHost.SignInPageHtml);

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert — the message alone has to carry the diagnosis, since that is all a log or a console shows.
            StringAssert.Contains(exception.Message, "https://example.com/api/auth/apikey");
            StringAssert.Contains(exception.Message, "200");
            StringAssert.Contains(exception.Message, "text/html; charset=utf-8");
            StringAssert.Contains(exception.Message, "Sign in to continue");
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersHtml_KeepsTheParserFailureAsInnerException()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            EnqueueHtmlResponse(handler, SignInPageHost.SignInPageHtml);

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert — a caller willing to dig reaches the parser's own account of the failure.
            Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentException));
            Assert.IsInstanceOfType(exception.InnerException?.InnerException, typeof(JsonException));
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_HandlerRecordsNoRequestMessage_ReportsTheEndpointWithoutAUri()
        {
            // Arrange — the shape that leaves the client with no address to name: nothing recorded the request.
            FakeHttpHandler handler = new FakeHttpHandler { OmitRequestMessage = true };
            EnqueueHtmlResponse(handler, SignInPageHost.SignInPageHtml);

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert — the message still has to read as a sentence, and must not claim an endpoint it does not know.
            Assert.IsNull(exception.RequestUri);
            StringAssert.StartsWith(exception.Message, "The auth endpoint answered 200 ");
        }

        [TestMethod]
        public async Task GetAsync_PreAuthorized_RefreshEndpointAnswersHtml_ThrowsNamingTheRefreshEndpoint()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            EnqueueHtmlResponse(handler, SignInPageHost.SignInPageHtml);
            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            AuthResponse expired = new AuthResponse("expired-token", DateTime.UtcNow.AddMinutes(-1).ToString("O"), "old-refresh");
            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, expired);

            // Act
            InvalidAuthResponseException exception = await Assert.ThrowsExceptionAsync<InvalidAuthResponseException>(
                () => client.GetAsync("api/test"));

            // Assert — the refresh path reaches a wrong host first, and has to report it as the same thing.
            Assert.AreEqual("https://example.com/api/auth/refresh", exception.RequestUri?.ToString());
            Assert.AreEqual(HttpStatusCode.OK, exception.StatusCode);
            Assert.AreEqual("text/html; charset=utf-8", exception.ContentType);
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_RefreshEndpointAnswersHtml_DoesNotFallBackToTheAuthEndpoint()
        {
            // Arrange — a client that *could* fall back: it holds credentials, so a refresh reported as merely
            // failed would re-authenticate against the same wrong address instead of reporting it.
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueJsonResponse(new AuthResponse("first-token", DateTime.UtcNow.AddMinutes(1).ToString("O"), "a-refresh-token").ToJson());
            handler.EnqueueJsonResponse("{\"data\":\"hello\"}");
            EnqueueHtmlResponse(handler, SignInPageHost.SignInPageHtml);

            // What a fall-back to full re-authentication would go on to consume. Enqueued so that the regression
            // shows up as the request sequence it is, rather than as the fake running out of answers.
            handler.EnqueueJsonResponse(new AuthResponse("second-token", DateTime.UtcNow.AddMinutes(60).ToString("O"), "another-refresh-token").ToJson());
            handler.EnqueueJsonResponse("{\"data\":\"hello\"}");

            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, "api-key");

            // Act — the first request authenticates and comes back with a token already inside the expiry margin,
            // so the second one refreshes.
            await client.GetAsync("api/test");

            InvalidAuthResponseException exception = await Assert.ThrowsExceptionAsync<InvalidAuthResponseException>(
                () => client.GetAsync("api/test"));

            // Assert
            Assert.AreEqual("https://example.com/api/auth/refresh", exception.RequestUri?.ToString());

            Assert.AreEqual(3, handler.SentRequests.Count, "The refresh must not be followed by a second authentication.");
            Assert.AreEqual("https://example.com/api/auth/apikey", handler.SentRequests[0].RequestUri?.ToString());
            Assert.AreEqual("https://example.com/api/test", handler.SentRequests[1].RequestUri?.ToString());
            Assert.AreEqual("https://example.com/api/auth/refresh", handler.SentRequests[2].RequestUri?.ToString());
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersJsonThatIsNotAnAuthResponse_Throws()
        {
            // Arrange — well-formed JSON the parser accepts, carrying none of the fields an auth response has.
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueJsonResponse("{\"error\":\"unknown route\"}");

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert
            Assert.AreEqual("https://example.com/api/auth/apikey", exception.RequestUri?.ToString());
            Assert.AreEqual("application/json; charset=utf-8", exception.ContentType);
            StringAssert.Contains(exception.BodySnippet, "unknown route");
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersAnUnreadableExpiry_ThrowsNamingTheEndpoint()
        {
            // Arrange — every field present, so only reading the expiry catches it. Reported anywhere else, this
            // arrives as a bare FormatException naming no address at all.
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueJsonResponse("{\"token\":\"header.payload.signature\",\"expiresAt\":\"tomorrow\"}");

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert
            Assert.AreEqual("https://example.com/api/auth/apikey", exception.RequestUri?.ToString());
            StringAssert.Contains(exception.Message, "tomorrow");
            Assert.IsFalse(exception.Message.Contains("header.payload.signature"), exception.Message);
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersATokenWithoutAnExpiry_RedactsTheTokenInTheSnippet()
        {
            // Arrange — the near miss that carries a real secret: an auth response but for the missing expiry.
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueJsonResponse("{\"token\":\"header.payload.signature\",\"refreshToken\":\"a-refresh-token\"}");

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert — the snippet is written to the same logs AuthResponse.ToString() redacts for.
            Assert.IsFalse(exception.BodySnippet.Contains("header.payload.signature"), exception.BodySnippet);
            Assert.IsFalse(exception.BodySnippet.Contains("a-refresh-token"), exception.BodySnippet);
            Assert.IsFalse(exception.Message.Contains("header.payload.signature"), exception.Message);
            StringAssert.Contains(exception.BodySnippet, "[REDACTED]");
            StringAssert.Contains(exception.BodySnippet, "token");
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersABodyCutOffAfterTheTokenValue_StillRedactsTheToken()
        {
            // Arrange — a body cut off mid-JSON parses as nothing, so a parser-based redaction would never see it.
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueJsonResponse("{\"expiresAt\":\"2026-01-01T00:00:00Z\",\"token\":\"header.payload.signature\"");

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert
            Assert.IsFalse(exception.BodySnippet.Contains("header.payload.signature"), exception.BodySnippet);
            StringAssert.Contains(exception.BodySnippet, "[REDACTED]");
            StringAssert.Contains(exception.BodySnippet, "2026-01-01T00:00:00Z");
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersABodyCutOffInsideTheTokenValue_StillRedactsTheToken()
        {
            // Arrange — the truncation that actually costs something: the body ends inside the token, so the value
            // has no closing quote. This is the whole secret, in a body no parser will hand back.
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueJsonResponse("{\"expiresAt\":\"2026-01-01T00:00:00Z\",\"token\":\"header.payload.signat");

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert
            Assert.IsFalse(exception.BodySnippet.Contains("header.payload.signat"), exception.BodySnippet);
            Assert.IsFalse(exception.Message.Contains("header.payload.signat"), exception.Message);
            StringAssert.Contains(exception.BodySnippet, "[REDACTED]");
            StringAssert.Contains(exception.BodySnippet, "2026-01-01T00:00:00Z");
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersATokenValueCarryingAnEscapedQuote_RedactsAllOfIt()
        {
            // Arrange — an escaped quote inside the value is not the end of the value, and a redaction that stops
            // there prints the rest of the token.
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueJsonResponse("{\"token\":\"header.payload\\\"signature\",\"error\":\"unknown route\"}");

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert
            Assert.IsFalse(exception.BodySnippet.Contains("signature"), exception.BodySnippet);
            Assert.IsFalse(exception.Message.Contains("signature"), exception.Message);
            StringAssert.Contains(exception.BodySnippet, "[REDACTED]");
            StringAssert.Contains(exception.BodySnippet, "unknown route");
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_SignInPageCarriesATokenInAHiddenField_RedactsIt()
        {
            // Arrange — the headline case, as the page actually arrives: a sign-in form whose hidden field holds
            // an antiforgery or OIDC token. The name and the value are separate attributes, so a rule that only
            // knows JSON prints the secret verbatim into the message that gets logged.
            FakeHttpHandler handler = new FakeHttpHandler();
            EnqueueHtmlResponse(
                handler,
                "<form action=\"/signin\">"
                + "<input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"CfDJ8SECRETVALUE\">"
                + "<input name=\"returnUrl\" value=\"/api/test\">"
                + "</form>");

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert
            Assert.IsFalse(exception.BodySnippet.Contains("CfDJ8SECRETVALUE"), exception.BodySnippet);
            Assert.IsFalse(exception.Message.Contains("CfDJ8SECRETVALUE"), exception.Message);
            StringAssert.Contains(exception.BodySnippet, "[REDACTED]");
            StringAssert.Contains(exception.BodySnippet, "/api/test", "A field that is not a secret still has to be readable.");
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_SignInPageCarriesATokenInAValueBeforeItsName_RedactsIt()
        {
            // Arrange — nothing says the value attribute comes second.
            FakeHttpHandler handler = new FakeHttpHandler();
            EnqueueHtmlResponse(handler, "<input value='CfDJ8SECRETVALUE' type='hidden' name='id_token'>");

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert
            Assert.IsFalse(exception.BodySnippet.Contains("CfDJ8SECRETVALUE"), exception.BodySnippet);
            StringAssert.Contains(exception.BodySnippet, "[REDACTED]");
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersATokenThatIsNotAString_RedactsIt()
        {
            // Arrange — a secret does not become safe to print by being written as an array or a bare number.
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueJsonResponse("{\"tokens\":[\"SECRETVALUE\"],\"secretCode\":12345,\"error\":\"unknown route\"}");

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert
            Assert.IsFalse(exception.BodySnippet.Contains("SECRETVALUE"), exception.BodySnippet);
            Assert.IsFalse(exception.BodySnippet.Contains("12345"), exception.BodySnippet);
            StringAssert.Contains(exception.BodySnippet, "unknown route");
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersALongBody_CapsTheSnippet()
        {
            // Arrange
            string longBody = new string('x', InvalidAuthResponseException.BodySnippetLength + 500);
            FakeHttpHandler handler = new FakeHttpHandler();
            EnqueueHtmlResponse(handler, longBody);

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert
            Assert.AreEqual(new string('x', InvalidAuthResponseException.BodySnippetLength) + "…", exception.BodySnippet);
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersABodyThatFits_SnippetCarriesNoTruncationMarker()
        {
            // Arrange — exactly the cap, so the marker appears only when something was actually left out.
            string body = new string('x', InvalidAuthResponseException.BodySnippetLength);
            FakeHttpHandler handler = new FakeHttpHandler();
            EnqueueHtmlResponse(handler, body);

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert
            Assert.AreEqual(body, exception.BodySnippet);
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersABodyBreakingAtTheCap_SnippetDoesNotEndOnASeparator()
        {
            // Arrange — the body runs out of room exactly where a collapsed space would go, so a snippet that
            // writes the separator anyway ends " …": a space that separates the text from nothing.
            string body = new string('x', InvalidAuthResponseException.BodySnippetLength - 1) + " tail";
            FakeHttpHandler handler = new FakeHttpHandler();
            EnqueueHtmlResponse(handler, body);

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert
            Assert.AreEqual(new string('x', InvalidAuthResponseException.BodySnippetLength - 1) + "…", exception.BodySnippet);
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersAMultiLineBody_CollapsesWhitespaceInTheSnippet()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            EnqueueHtmlResponse(handler, "  <html>\n\n\t<body>Sign   in</body>\n</html>  ");

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert — a page spread over many lines would otherwise bury the rest of the message.
            Assert.AreEqual("<html> <body>Sign in</body> </html>", exception.BodySnippet);
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersAnEmptyBody_SaysTheBodyWasEmpty()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            EnqueueHtmlResponse(handler, string.Empty);

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert
            Assert.AreEqual(string.Empty, exception.BodySnippet);
            StringAssert.Contains(exception.Message, "The body was empty.");
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersNothingButWhitespace_SaysTheBodyWasEmpty()
        {
            // Arrange — a different path from the empty body: this one runs the collapsing loop and has to come out
            // of it with nothing, rather than with a snippet made of spaces.
            FakeHttpHandler handler = new FakeHttpHandler();
            EnqueueHtmlResponse(handler, "   \n\t  \r\n ");

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert
            Assert.AreEqual(string.Empty, exception.BodySnippet);
            StringAssert.Contains(exception.Message, "The body was empty.");
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersWithoutAContentType_ReportsNoContentType()
        {
            // Arrange
            FakeHttpHandler handler = new FakeHttpHandler();
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not json"),
            };
            response.Content.Headers.ContentType = null;
            handler.EnqueueResponse(response);

            // Act
            InvalidAuthResponseException exception = await CaptureApiKeyFailureAsync(handler);

            // Assert
            Assert.IsNull(exception.ContentType);
            StringAssert.Contains(exception.Message, "no content type");
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointRejectsWithABodyCarryingAToken_KeepsItOutOfTheMessage()
        {
            // Arrange — an unsuccessful status is reported before the body is ever parsed, and that branch builds a
            // message out of the body too. A 401 carrying a token is the shape that costs something there.
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(
                    "{\"error\":\"token expired\",\"refreshToken\":\"a-refresh-token\"}",
                    System.Text.Encoding.UTF8,
                    "application/json"),
            });
            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, "api-key");

            // Act
            UnauthorizedAccessException exception = await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(
                () => client.GetAsync("api/test"));

            // Assert
            Assert.IsFalse(exception.Message.Contains("a-refresh-token"), exception.Message);
            StringAssert.Contains(exception.Message, "[REDACTED]");
            StringAssert.Contains(exception.Message, "token expired");
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointFailsWithALongBody_CapsItInTheMessage()
        {
            // Arrange — the body comes from the address that is answering wrongly, so its length is not something
            // the client gets to take on trust.
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(new string('x', 200_000), System.Text.Encoding.UTF8, "text/plain"),
            });
            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, "api-key");

            // Act
            HttpRequestException exception = await Assert.ThrowsExceptionAsync<HttpRequestException>(
                () => client.GetAsync("api/test"));

            // Assert
            Assert.IsTrue(
                exception.Message.Length < InvalidAuthResponseException.BodySnippetLength + 200,
                $"The message is {exception.Message.Length} characters long.");
            StringAssert.Contains(exception.Message, "…");
        }

        [TestMethod]
        public async Task GetAsync_ApiKey_AuthEndpointAnswersAnAuthResponse_DoesNotThrow()
        {
            // Arrange — the same path with a real auth response, so the new failure cannot be what always happens.
            FakeHttpHandler handler = new FakeHttpHandler();
            handler.EnqueueJsonResponse(new AuthResponse("a-token", DateTime.UtcNow.AddMinutes(60).ToString("O"), "a-refresh-token").ToJson());
            handler.EnqueueJsonResponse("{\"data\":\"hello\"}");
            HttpClient httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            using AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, "api-key");

            // Act
            HttpResponseMessage response = await client.GetAsync("api/test");

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
