using System.Net;

namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// Builds the clients, auth responses and status responses the refusal and concurrency tests share.
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
        /// Builds an auth response expiring the given number of minutes from now.
        /// </summary>
        /// <param name="token">The access token.</param>
        /// <param name="expiresInMinutes">Minutes until expiry; negative for an expired token.</param>
        /// <param name="refreshToken">The refresh token, or <c>null</c> for none.</param>
        /// <returns>The auth response.</returns>
        public static AuthResponse CreateAuthResponse(string token, int expiresInMinutes, string? refreshToken)
        {
            string expiresAt = DateTime.UtcNow.AddMinutes(expiresInMinutes).ToString("O");
            return new AuthResponse(token, expiresAt, refreshToken);
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

        /// <summary>
        /// Builds a successful response carrying <paramref name="authResponse"/> as JSON.
        /// </summary>
        /// <param name="authResponse">The auth response to carry.</param>
        /// <returns>The response.</returns>
        public static HttpResponseMessage CreateAuthResponseMessage(AuthResponse authResponse)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(authResponse.ToJson(), System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}
