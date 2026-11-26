using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// Redeems an authorization code at a provider's token endpoint as a confidential client, with the flow's redirect URI
    /// and PKCE verifier.
    /// </summary>
    internal sealed class OpenIdConnectTokenEndpointClient
    {
        /// <summary>
        /// The token endpoint errors that refuse this one code rather than the client (RFC 6749 section 5.2):
        /// <c>invalid_grant</c> for a code already used, expired, or issued for another redirect URI or verifier, and
        /// <c>invalid_request</c>, which some providers answer for a malformed code.
        /// </summary>
        private static readonly IReadOnlySet<string> RefusalErrors = new HashSet<string>(StringComparer.Ordinal) { "invalid_grant", "invalid_request" };

        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenIdConnectTokenEndpointClient"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Creates the package's named HTTP client.</param>
        public OpenIdConnectTokenEndpointClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Redeems a code for an ID token.
        /// </summary>
        /// <param name="provider">The provider, for the client credentials and how to send them.</param>
        /// <param name="tokenEndpoint">The token endpoint from the provider's discovery document.</param>
        /// <param name="code">The authorization code.</param>
        /// <param name="redirectUri">The redirect URI the flow named.</param>
        /// <param name="codeVerifier">The flow's PKCE verifier.</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>The raw ID token, or null when the provider refused the code (a 400 or 401 with
        /// <c>invalid_grant</c> or <c>invalid_request</c>) or answered without an ID token.</returns>
        /// <exception cref="InvalidOperationException">The endpoint answered 400 or 401 with any other error, or none,
        /// such as <c>invalid_client</c>: the client itself is refused.</exception>
        /// <exception cref="HttpRequestException">The endpoint could not be reached or answered with any other
        /// unsuccessful status, such as a server error or a 429 throttle.</exception>
        public async Task<string?> RedeemCodeAsync(
            OpenIdConnectProvider provider,
            string tokenEndpoint,
            string code,
            string redirectUri,
            string codeVerifier,
            CancellationToken cancellationToken)
        {
            Dictionary<string, string> form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier,
            };

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);

            if (provider.ClientAuthentication == OpenIdConnectClientAuthentication.ClientSecretPost)
            {
                form["client_id"] = provider.ClientId;
                form["client_secret"] = provider.ClientSecret;
            }
            else
            {
                // RFC 6749 section 2.3.1: each part is form-encoded before the two are joined and base64-encoded.
                string credentials = $"{WebUtility.UrlEncode(provider.ClientId)}:{WebUtility.UrlEncode(provider.ClientSecret)}";
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
            }

            request.Content = new FormUrlEncodedContent(form);

            HttpClient client = _httpClientFactory.CreateClient(OpenIdConnectCodeFlow.HttpClientName);
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Unauthorized)
            {
                string? error = ReadStringProperty(await response.Content.ReadAsStringAsync(cancellationToken), "error");
                if (RefusalErrors.Contains(error ?? ""))
                {
                    return null;
                }

                // invalid_client, unauthorized_client and the rest say this server's client is set up wrong (an expired
                // secret, a grant the client may not use): every sign-in would fail, so it is an outage, not a refusal.
                throw new InvalidOperationException(
                    $"The token endpoint '{tokenEndpoint}' refused the client with {(int)response.StatusCode} and error '{error ?? "(none)"}'. Check the provider's client id, secret and client authentication method.");
            }

            response.EnsureSuccessStatusCode();

            return ReadStringProperty(await response.Content.ReadAsStringAsync(cancellationToken), "id_token");
        }

        /// <summary>
        /// A non-empty string property of a JSON object body, or null when the body is not such an object or lacks it.
        /// </summary>
        private static string? ReadStringProperty(string body, string name)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty(name, out JsonElement property)
                    && property.ValueKind == JsonValueKind.String)
                {
                    string? value = property.GetString();
                    return string.IsNullOrEmpty(value) ? null : value;
                }

                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
