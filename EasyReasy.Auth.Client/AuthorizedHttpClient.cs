namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// An HTTP client that automatically handles authentication using API keys or username/password.
    /// </summary>
    public class AuthorizedHttpClient : IDisposable
    {
        /// <summary>
        /// The type of authentication being used.
        /// </summary>
        public enum AuthType
        {
            /// <summary>
            /// API key authentication.
            /// </summary>
            ApiKey,

            /// <summary>
            /// Username/password authentication.
            /// </summary>
            UsernamePassword
        }

        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly string? _username;
        private readonly string? _password;
        private readonly AuthType _authType;
        private bool _disposed;
        private bool _isAuthorized;
        private DateTime? _tokenExpiresAt;
        private readonly string _authEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizedHttpClient"/> class with API key authentication.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for requests.</param>
        /// <param name="apiKey">The API key for authentication.</param>
        /// <param name="authEndpoint">The authentication endpoint path. If not specified, defaults to "/api/auth/apikey".</param>
        public AuthorizedHttpClient(HttpClient httpClient, string apiKey, string? authEndpoint = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            if (_httpClient.BaseAddress?.ToString().LastOrDefault() is char lastCharacter && lastCharacter != '/')
                _httpClient.BaseAddress = new Uri(_httpClient.BaseAddress.ToString() + "/");

            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _authEndpoint = authEndpoint ?? "api/auth/apikey";
            _authType = AuthType.ApiKey;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizedHttpClient"/> class with username/password authentication.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for requests.</param>
        /// <param name="username">The username for authentication.</param>
        /// <param name="password">The password for authentication.</param>
        /// <param name="authEndpoint">The authentication endpoint path. If not specified, defaults to "/api/auth/login".</param>
        public AuthorizedHttpClient(HttpClient httpClient, string username, string password, string? authEndpoint = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _username = username ?? throw new ArgumentNullException(nameof(username));
            _password = password ?? throw new ArgumentNullException(nameof(password));
            _authEndpoint = authEndpoint ?? "api/auth/login";
            _authType = AuthType.UsernamePassword;
        }

        /// <summary>
        /// Gets the underlying HTTP client.
        /// </summary>
        public HttpClient HttpClient => _httpClient;

        /// <summary>
        /// Gets the type of authentication being used.
        /// </summary>
        public AuthType AuthenticationType => _authType;

        /// <summary>
        /// Creates a new HttpClient with the specified base address.
        /// </summary>
        /// <param name="baseAddress">The base address for the HTTP client.</param>
        /// <returns>A new HttpClient instance with the specified base address.</returns>
        public static HttpClient CreateHttpClient(string baseAddress)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(baseAddress);
            return httpClient;
        }

        /// <summary>
        /// Ensures the client is authorized and the token is not expired.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task EnsureAuthorizedAsync(CancellationToken cancellationToken = default)
        {
            // Check if we need to authorize or re-authorize
            if (!_isAuthorized || IsTokenExpired())
            {
                await AuthorizeAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Checks if the current token is expired.
        /// </summary>
        /// <returns>True if the token is expired or will expire within 5 minutes; otherwise, false.</returns>
        private bool IsTokenExpired()
        {
            if (!_tokenExpiresAt.HasValue)
                return true;

            // Consider token expired if it expires within 5 minutes
            return _tokenExpiresAt.Value <= DateTime.UtcNow.AddMinutes(5);
        }

        /// <summary>
        /// Authorizes the client using the configured authentication method and obtains a JWT token.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when authentication fails.</exception>
        private async Task AuthorizeAsync(CancellationToken cancellationToken = default)
        {
            string json;
            string endpoint;

            switch (_authType)
            {
                case AuthType.ApiKey:
                    ApiKeyAuthRequest apiKeyRequest = new ApiKeyAuthRequest(_apiKey!);
                    json = apiKeyRequest.ToJson();
                    endpoint = _authEndpoint;
                    break;

                case AuthType.UsernamePassword:
                    LoginAuthRequest loginRequest = new LoginAuthRequest(_username!, _password!);
                    json = loginRequest.ToJson();
                    endpoint = _authEndpoint;
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported authentication type: {_authType}");
            }

            StringContent content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            string postUrl = _httpClient.BaseAddress?.ToString() ?? string.Empty;
            HttpResponseMessage response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException($"Authentication failed with status {response.StatusCode}. Additional information from the backend: {errorContent}");
                }

                throw new HttpRequestException($"Authentication failed. Status: {response.StatusCode}, Content: {errorContent}");
            }

            string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            AuthResponse authResponse = AuthResponse.FromJson(responseJson);

            // Set the authorization header
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);
            _isAuthorized = true;
            _tokenExpiresAt = DateTime.Parse(authResponse.ExpiresAt);
        }

        /// <summary>
        /// Sends an HTTP request and ensures authorization before sending.
        /// </summary>
        /// <param name="request">The HTTP request message.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The HTTP response message.</returns>
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            await EnsureAuthorizedAsync(cancellationToken);

            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Token might be expired, try to re-authorize once
                await AuthorizeAsync(cancellationToken);
                response = await _httpClient.SendAsync(request, cancellationToken);
            }

            return response;
        }

        /// <summary>
        /// Sends a GET request and ensures authorization before sending.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The HTTP response message.</returns>
        public async Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken = default)
        {
            await EnsureAuthorizedAsync(cancellationToken);

            HttpResponseMessage response = await _httpClient.GetAsync(requestUri, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Token might be expired, try to re-authorize once
                await AuthorizeAsync(cancellationToken);
                response = await _httpClient.GetAsync(requestUri, cancellationToken);
            }

            return response;
        }

        /// <summary>
        /// Sends a POST request and ensures authorization before sending.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The HTTP content.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The HTTP response message.</returns>
        public async Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content, CancellationToken cancellationToken = default)
        {
            await EnsureAuthorizedAsync(cancellationToken);

            HttpResponseMessage response = await _httpClient.PostAsync(requestUri, content, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Token might be expired, try to re-authorize once
                await AuthorizeAsync(cancellationToken);
                response = await _httpClient.PostAsync(requestUri, content, cancellationToken);
            }

            return response;
        }

        /// <summary>
        /// Disposes of the client. Note that the HttpClient is not disposed as it's managed externally.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Don't dispose the HttpClient as it's managed externally
                _disposed = true;
            }
        }
    }
}