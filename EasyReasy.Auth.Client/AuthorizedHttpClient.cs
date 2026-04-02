using System.Globalization;

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
            UsernamePassword,

            /// <summary>
            /// Pre-authorized with an existing token and optional refresh token.
            /// </summary>
            PreAuthorized
        }

        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly string? _username;
        private readonly string? _password;
        private readonly AuthType _authType;
        private readonly string _authEndpoint;
        private readonly string _refreshEndpoint;
        private readonly Action<AuthResponse>? _onAuthResponseChanged;
        private readonly SemaphoreSlim _authLock = new SemaphoreSlim(1, 1);
        private string? _refreshToken;
        private DateTime? _tokenExpiresAt;
        private bool _isAuthorized;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizedHttpClient"/> class with API key authentication.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for requests.</param>
        /// <param name="apiKey">The API key for authentication.</param>
        /// <param name="authEndpoint">The authentication endpoint path. If not specified, defaults to "/api/auth/apikey".</param>
        /// <param name="refreshEndpoint">The refresh token endpoint path. If not specified, defaults to "/api/auth/refresh".</param>
        /// <param name="onAuthResponseChanged">An optional callback invoked whenever the auth state changes (initial auth, token refresh, or re-auth).</param>
        public AuthorizedHttpClient(
            HttpClient httpClient,
            string apiKey,
            string? authEndpoint = null,
            string? refreshEndpoint = null,
            Action<AuthResponse>? onAuthResponseChanged = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            if (_httpClient.BaseAddress?.ToString().LastOrDefault() is char lastCharacter && lastCharacter != '/')
                _httpClient.BaseAddress = new Uri(_httpClient.BaseAddress.ToString() + "/");

            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _authEndpoint = authEndpoint ?? "api/auth/apikey";
            _refreshEndpoint = refreshEndpoint ?? "api/auth/refresh";
            _authType = AuthType.ApiKey;
            _onAuthResponseChanged = onAuthResponseChanged;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizedHttpClient"/> class with username/password authentication.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for requests.</param>
        /// <param name="username">The username for authentication.</param>
        /// <param name="password">The password for authentication.</param>
        /// <param name="authEndpoint">The authentication endpoint path. If not specified, defaults to "/api/auth/login".</param>
        /// <param name="refreshEndpoint">The refresh token endpoint path. If not specified, defaults to "/api/auth/refresh".</param>
        /// <param name="onAuthResponseChanged">An optional callback invoked whenever the auth state changes (initial auth, token refresh, or re-auth).</param>
        public AuthorizedHttpClient(
            HttpClient httpClient,
            string username,
            string password,
            string? authEndpoint = null,
            string? refreshEndpoint = null,
            Action<AuthResponse>? onAuthResponseChanged = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _username = username ?? throw new ArgumentNullException(nameof(username));
            _password = password ?? throw new ArgumentNullException(nameof(password));
            _authEndpoint = authEndpoint ?? "api/auth/login";
            _refreshEndpoint = refreshEndpoint ?? "api/auth/refresh";
            _authType = AuthType.UsernamePassword;
            _onAuthResponseChanged = onAuthResponseChanged;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizedHttpClient"/> class with an existing token.
        /// This is useful when the token and refresh token have been persisted from a previous session.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for requests.</param>
        /// <param name="authResponse">The authentication response containing the token, expiration, and optional refresh token.</param>
        /// <param name="refreshEndpoint">The refresh token endpoint path. If not specified, defaults to "/api/auth/refresh".</param>
        /// <param name="onAuthResponseChanged">An optional callback invoked whenever the auth state changes (initial auth, token refresh, or re-auth).</param>
        public AuthorizedHttpClient(
            HttpClient httpClient,
            AuthResponse authResponse,
            string? refreshEndpoint = null,
            Action<AuthResponse>? onAuthResponseChanged = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            if (_httpClient.BaseAddress?.ToString().LastOrDefault() is char lastCharacter && lastCharacter != '/')
                _httpClient.BaseAddress = new Uri(_httpClient.BaseAddress.ToString() + "/");

            ArgumentNullException.ThrowIfNull(authResponse);
            _authEndpoint = string.Empty;
            _refreshEndpoint = refreshEndpoint ?? "api/auth/refresh";
            _authType = AuthType.PreAuthorized;
            _onAuthResponseChanged = onAuthResponseChanged;

            ApplyAuthResponse(authResponse);
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
        /// Gets or sets the optional client identifier associated with the API key.
        /// Only used when <see cref="AuthenticationType"/> is <see cref="AuthType.ApiKey"/>.
        /// </summary>
        public string? ClientId { get; set; }

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
            // Quick check before acquiring the lock
            if (_isAuthorized && !IsTokenExpired())
            {
                return;
            }

            await _authLock.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring the lock — another thread may have already authorized
                if (!_isAuthorized || IsTokenExpired())
                {
                    await AuthorizeAsync(cancellationToken);
                }
            }
            finally
            {
                _authLock.Release();
            }
        }

        /// <summary>
        /// Forces a fresh authorization regardless of current authorization state.
        /// This is useful when the server rejects a token that the client thinks is still valid.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task ForceAuthorizeAsync(CancellationToken cancellationToken = default)
        {
            await _authLock.WaitAsync(cancellationToken);
            try
            {
                await AuthorizeAsync(cancellationToken);
            }
            finally
            {
                _authLock.Release();
            }
        }

        /// <summary>
        /// Clears the current authorization state and forces a fresh authorization.
        /// This is useful when you want to ensure a completely fresh authentication flow.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task ForceReauthorizeAsync(CancellationToken cancellationToken = default)
        {
            await _authLock.WaitAsync(cancellationToken);
            try
            {
                _isAuthorized = false;
                _tokenExpiresAt = null;
                _refreshToken = null;
                _httpClient.DefaultRequestHeaders.Authorization = null;
                await AuthorizeAsync(cancellationToken);
            }
            finally
            {
                _authLock.Release();
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
        /// If a refresh token is available, attempts to refresh first before falling back to full re-authentication.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when authentication fails.</exception>
        private async Task AuthorizeAsync(CancellationToken cancellationToken = default)
        {
            // Try refresh token first if available
            if (_refreshToken != null)
            {
                bool refreshed = await TryRefreshTokenAsync(cancellationToken);
                if (refreshed)
                {
                    return;
                }
            }

            string json;
            string endpoint;

            switch (_authType)
            {
                case AuthType.ApiKey:
                    ApiKeyAuthRequest apiKeyRequest = new ApiKeyAuthRequest(_apiKey!, ClientId);
                    json = apiKeyRequest.ToJson();
                    endpoint = _authEndpoint;
                    break;

                case AuthType.UsernamePassword:
                    LoginAuthRequest loginRequest = new LoginAuthRequest(_username!, _password!);
                    json = loginRequest.ToJson();
                    endpoint = _authEndpoint;
                    break;

                case AuthType.PreAuthorized:
                    throw new InvalidOperationException(
                        "Cannot re-authenticate a pre-authorized client. " +
                        "The token has expired and no refresh token is available. " +
                        "Create a new client with valid credentials.");

                default:
                    throw new InvalidOperationException($"Unsupported authentication type: {_authType}");
            }

            StringContent content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

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

            ApplyAuthResponse(authResponse);
        }

        /// <summary>
        /// Attempts to refresh the access token using the stored refresh token.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the refresh was successful; false if it failed and full re-authentication is needed.</returns>
        private async Task<bool> TryRefreshTokenAsync(CancellationToken cancellationToken = default)
        {
            RefreshRequest refreshRequest = new RefreshRequest(_refreshToken!);
            StringContent content = new StringContent(refreshRequest.ToJson(), System.Text.Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(_refreshEndpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Refresh failed — clear the refresh token and fall back to full re-auth
                _refreshToken = null;
                return false;
            }

            string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            AuthResponse authResponse = AuthResponse.FromJson(responseJson);

            ApplyAuthResponse(authResponse);
            return true;
        }

        /// <summary>
        /// Applies an authentication response by setting the authorization header, token expiration, and refresh token.
        /// </summary>
        /// <param name="authResponse">The authentication response to apply.</param>
        private void ApplyAuthResponse(AuthResponse authResponse)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);
            _isAuthorized = true;
            _tokenExpiresAt = DateTime.Parse(authResponse.ExpiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            _refreshToken = authResponse.RefreshToken;

            try
            {
                _onAuthResponseChanged?.Invoke(authResponse);
            }
            catch
            {
                // Swallow exceptions from the callback to prevent breaking the auth flow.
            }
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
                await ForceAuthorizeAsync(cancellationToken);
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
                await ForceAuthorizeAsync(cancellationToken);
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
                await ForceAuthorizeAsync(cancellationToken);
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
                _authLock.Dispose();
                // Don't dispose the HttpClient as it's managed externally
                _disposed = true;
            }
        }
    }
}