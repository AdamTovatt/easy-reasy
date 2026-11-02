using System.Net;

namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// Builds the clients, auth responses and status responses the client tests share, and holds the status lists
    /// that pin the refusal rule.
    /// </summary>
    public static class ClientTestFactory
    {
        /// <summary>
        /// The refresh endpoint's address for a client built by this factory.
        /// </summary>
        public const string RefreshUri = "https://example.com/api/auth/refresh";

        /// <summary>
        /// The API-key sign-in endpoint's address for a client built by this factory.
        /// </summary>
        public const string ApiKeySignInUri = "https://example.com/api/auth/apikey";

        /// <summary>
        /// The body <see cref="CreateStatusResponse"/> answers with, so a test can check it was quoted in a message.
        /// </summary>
        public const string StatusResponseBody = "server says no";

        /// <summary>
        /// Statuses the sign-in and refresh endpoints refuse with: a <c>4xx</c> other than <c>408</c> and
        /// <c>429</c>, including both ends of that range. The one list every refusal test reads, so a change to the
        /// rule cannot leave an outdated copy passing.
        /// </summary>
        public static IEnumerable<object[]> RefusalStatuses =>
        [
            [HttpStatusCode.BadRequest],
            [HttpStatusCode.Unauthorized],
            [HttpStatusCode.Forbidden],
            [HttpStatusCode.NotFound],
            [(HttpStatusCode)499],
        ];

        /// <summary>
        /// Unsuccessful statuses that are the server failing rather than refusing: an unfollowed <c>3xx</c>,
        /// <c>408</c>, <c>429</c> and the <c>5xx</c> range. The one list every server-failure test reads.
        /// </summary>
        public static IEnumerable<object[]> ServerFailureStatuses =>
        [
            [HttpStatusCode.Found],
            [HttpStatusCode.RequestTimeout],
            [HttpStatusCode.TooManyRequests],
            [HttpStatusCode.InternalServerError],
            [HttpStatusCode.ServiceUnavailable],
            [(HttpStatusCode)599],
        ];

        /// <summary>
        /// Builds an auth response expiring the given number of minutes from now.
        /// </summary>
        /// <param name="token">The access token.</param>
        /// <param name="expiresInMinutes">Minutes until expiry; negative for an expired token.</param>
        /// <param name="refreshToken">The refresh token, or <c>null</c> for none.</param>
        /// <returns>The auth response.</returns>
        public static AuthResponse CreateAuthResponse(string token = "test-token", int expiresInMinutes = 60, string? refreshToken = "test-refresh")
        {
            string expiresAt = DateTime.UtcNow.AddMinutes(expiresInMinutes).ToString("O");
            return new AuthResponse(token, expiresAt, refreshToken);
        }

        /// <summary>
        /// Builds an auth response as <see cref="CreateAuthResponse"/> does and writes it as JSON.
        /// </summary>
        /// <param name="token">The access token.</param>
        /// <param name="expiresInMinutes">Minutes until expiry; negative for an expired token.</param>
        /// <param name="refreshToken">The refresh token, or <c>null</c> for none.</param>
        /// <returns>The auth response as JSON.</returns>
        public static string CreateAuthResponseJson(string token = "test-token", int expiresInMinutes = 60, string? refreshToken = "test-refresh")
        {
            return CreateAuthResponse(token, expiresInMinutes, refreshToken).ToJson();
        }

        /// <summary>
        /// Builds an <see cref="HttpClient"/> over <paramref name="handler"/> at <c>https://example.com/</c>.
        /// </summary>
        /// <param name="handler">The handler answering the client's requests.</param>
        /// <returns>The HTTP client.</returns>
        public static HttpClient CreateHttpClient(FakeHttpHandler handler)
        {
            return new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
        }

        /// <summary>
        /// Builds a client constructed from an expired auth response, so its next call redeems the refresh token.
        /// </summary>
        /// <param name="handler">The handler answering the client's requests.</param>
        /// <param name="refreshToken">The refresh token, or <c>null</c> for none.</param>
        /// <returns>The client.</returns>
        public static AuthorizedHttpClient CreateExpiredPreAuthorizedClient(FakeHttpHandler handler, string? refreshToken)
        {
            return new AuthorizedHttpClient(CreateHttpClient(handler), CreateAuthResponse("expired-token", -1, refreshToken));
        }

        /// <summary>
        /// Builds a response with the given status and a short text body.
        /// </summary>
        /// <param name="statusCode">The status.</param>
        /// <returns>The response.</returns>
        public static HttpResponseMessage CreateStatusResponse(HttpStatusCode statusCode)
        {
            return new HttpResponseMessage(statusCode) { Content = new StringContent(StatusResponseBody) };
        }
    }
}
