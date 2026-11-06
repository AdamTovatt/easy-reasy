using System.Net;
using System.Text.Json;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// Redeems an authorization code at Microsoft's token endpoint as a confidential client (client id and secret),
    /// with the flow's redirect URI and PKCE verifier.
    /// </summary>
    internal sealed class MicrosoftTokenEndpointClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly MicrosoftAuthOptions _options;
        private readonly MicrosoftAuthority _authority;

        /// <summary>
        /// Initializes a new instance of the <see cref="MicrosoftTokenEndpointClient"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Creates the package's named HTTP client.</param>
        /// <param name="options">The Microsoft options, for the client credentials.</param>
        /// <param name="authority">The authority, for the token endpoint.</param>
        public MicrosoftTokenEndpointClient(IHttpClientFactory httpClientFactory, MicrosoftAuthOptions options, MicrosoftAuthority authority)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _authority = authority;
        }

        /// <summary>
        /// Redeems a code for an ID token.
        /// </summary>
        /// <param name="tenantId">The tenant the flow requires, or null for the <c>organizations</c> segment.</param>
        /// <param name="code">The authorization code.</param>
        /// <param name="redirectUri">The redirect URI the flow named.</param>
        /// <param name="codeVerifier">The flow's PKCE verifier.</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>The raw ID token, or null when Microsoft refused the code (a 400 or 401, which is how it answers
        /// an <c>invalid_grant</c> or <c>invalid_client</c>) or answered without an ID token.</returns>
        /// <exception cref="HttpRequestException">The endpoint could not be reached or answered with any other
        /// unsuccessful status, such as a server error or a 429 throttle.</exception>
        public async Task<string?> RedeemCodeAsync(
            Guid? tenantId,
            string code,
            string redirectUri,
            string codeVerifier,
            CancellationToken cancellationToken)
        {
            Dictionary<string, string> form = new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier,
                ["scope"] = MicrosoftAuthority.Scope,
            };

            HttpClient client = _httpClientFactory.CreateClient(MicrosoftAuthServiceCollectionExtensions.HttpClientName);
            using FormUrlEncodedContent content = new FormUrlEncodedContent(form);
            using HttpResponseMessage response = await client.PostAsync(_authority.GetTokenEndpoint(tenantId), content, cancellationToken);

            if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            return ReadIdToken(body);
        }

        private static string? ReadIdToken(string body)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("id_token", out JsonElement idToken)
                    && idToken.ValueKind == JsonValueKind.String)
                {
                    string? value = idToken.GetString();
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
