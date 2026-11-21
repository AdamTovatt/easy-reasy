using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// Drives an app's EasyReasy.Auth OAuth authorization server the way an MCP client and the app's consent page do, step
    /// by step, over the app's own test <see cref="HttpClient"/>: register, authorize, read and approve (or deny) the
    /// pending request as a signed-in person, redeem the code, refresh, revoke. Each step checks what a client would check
    /// (the redirect's <c>state</c> and <c>iss</c>) and throws <see cref="OAuthTestFlowException"/> on any other answer, so
    /// a test of an app's grant handler asserts on what the handler decided.
    /// </summary>
    /// <remarks>
    /// The <see cref="HttpClient"/> must not follow redirects (a <c>TestServer</c> client does not), since the authorization
    /// step reads the redirect to the consent page. The consent page itself is the app's: where it would sign the person in,
    /// the test passes an access token from its own sign-in.
    /// </remarks>
    public sealed class OAuthTestClient
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes the client.
        /// </summary>
        /// <param name="httpClient">A client for the app under test that does not follow redirects.</param>
        public OAuthTestClient(HttpClient httpClient)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            _httpClient = httpClient;
        }

        /// <summary>
        /// Registers a client at <c>/oauth/register</c> (RFC 7591).
        /// </summary>
        /// <param name="redirectUri">The redirect URI to register and use.</param>
        /// <param name="authenticationMethod">How the client authenticates; public (<see cref="OAuthClientAuthenticationMethod.None"/>) by default, as MCP clients register.</param>
        /// <param name="clientName">The name to register, or null.</param>
        /// <returns>The registered client.</returns>
        /// <exception cref="OAuthTestFlowException">The server refused the registration.</exception>
        public async Task<OAuthTestRegisteredClient> RegisterAsync(
            string redirectUri,
            OAuthClientAuthenticationMethod authenticationMethod = OAuthClientAuthenticationMethod.None,
            string? clientName = null)
        {
            ArgumentNullException.ThrowIfNull(redirectUri);
            Dictionary<string, object?> body = new Dictionary<string, object?>
            {
                ["redirect_uris"] = new[] { redirectUri },
                ["token_endpoint_auth_method"] = OAuthClientAuthenticationMethodNames.ToName(authenticationMethod),
                ["grant_types"] = new[] { "authorization_code", "refresh_token" },
                ["response_types"] = new[] { "code" },
                ["client_name"] = clientName,
            };

            using HttpResponseMessage response = await _httpClient.PostAsync(
                OAuthServerPaths.Register,
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
            string content = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.Created)
            {
                throw FromBody("The registration was refused.", response.StatusCode, content);
            }

            using JsonDocument document = JsonDocument.Parse(content);
            JsonElement root = document.RootElement;
            return new OAuthTestRegisteredClient
            {
                ClientId = root.GetProperty("client_id").GetString()!,
                ClientSecret = root.TryGetProperty("client_secret", out JsonElement secret) ? secret.GetString() : null,
                AuthenticationMethod = authenticationMethod,
                RedirectUri = redirectUri,
            };
        }

        /// <summary>
        /// Sends an authorization request to <c>/oauth/authorize</c> with a fresh PKCE verifier and state, and reads the
        /// redirect to the app's consent page.
        /// </summary>
        /// <param name="client">The client asking.</param>
        /// <param name="resource">The <c>resource</c> to name, or null. MCP clients name the server's resource.</param>
        /// <param name="scope">The space-delimited scope to request, or null.</param>
        /// <param name="redirectUri">A redirect URI other than the client's, to test the server's refusal of it; null uses the client's.</param>
        /// <returns>The pending authorization.</returns>
        /// <exception cref="OAuthTestFlowException">The server answered with an error, on the page or by redirecting to the client.</exception>
        public async Task<OAuthTestAuthorization> AuthorizeAsync(
            OAuthTestRegisteredClient client,
            string? resource = null,
            string? scope = null,
            string? redirectUri = null)
        {
            ArgumentNullException.ThrowIfNull(client);
            string codeVerifier = OAuthTestPkce.CreateVerifier();
            string state = OAuthTestPkce.CreateVerifier();
            string usedRedirectUri = redirectUri ?? client.RedirectUri;

            Dictionary<string, string?> query = new Dictionary<string, string?>
            {
                ["response_type"] = "code",
                ["client_id"] = client.ClientId,
                ["redirect_uri"] = usedRedirectUri,
                ["code_challenge"] = OAuthTestPkce.ChallengeFor(codeVerifier),
                ["code_challenge_method"] = "S256",
                ["state"] = state,
                ["scope"] = scope,
                ["resource"] = resource,
            };

            string authorizeUrl = QueryHelpers.AddQueryString(OAuthServerPaths.Authorize, query.Where(parameter => parameter.Value != null));
            using HttpResponseMessage response = await _httpClient.GetAsync(authorizeUrl);
            string content = await response.Content.ReadAsStringAsync();
            Uri? location = response.Headers.Location;
            if (!IsRedirect(response.StatusCode) || location == null)
            {
                throw FromBody("The authorization request was refused without a redirect.", response.StatusCode, content);
            }

            string locationText = location.OriginalString;
            if (locationText.StartsWith(usedRedirectUri, StringComparison.Ordinal))
            {
                Dictionary<string, string> errorParameters = ParseQuery(locationText);
                throw new OAuthTestFlowException(
                    "The authorization request was refused with a redirect to the client.",
                    response.StatusCode,
                    errorParameters.GetValueOrDefault("error"),
                    errorParameters.GetValueOrDefault("error_description"),
                    content);
            }

            Dictionary<string, string> consentParameters = ParseQuery(locationText);
            if (!consentParameters.TryGetValue("request", out string? requestId))
            {
                throw new OAuthTestFlowException($"The redirect to '{locationText}' carries no request parameter.", response.StatusCode, null, null, content);
            }

            return new OAuthTestAuthorization
            {
                Client = client,
                RequestId = requestId,
                ConsentPageUrl = locationText,
                RedirectUri = usedRedirectUri,
                CodeVerifier = codeVerifier,
                State = state,
                Resource = resource,
            };
        }

        /// <summary>
        /// Reads what a pending request asks, as the consent page does.
        /// </summary>
        /// <param name="authorization">The pending authorization.</param>
        /// <param name="accessToken">An access token from the app's own sign-in, for the person on the consent page.</param>
        /// <returns>The request's details.</returns>
        /// <exception cref="OAuthTestFlowException">The server did not answer 200.</exception>
        public async Task<OAuthAuthorizationRequestDetails> GetRequestAsync(OAuthTestAuthorization authorization, string accessToken)
        {
            ArgumentNullException.ThrowIfNull(authorization);
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, RequestPath(authorization));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            string content = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw FromBody("The request's details were not returned.", response.StatusCode, content);
            }

            return OAuthAuthorizationRequestDetails.FromJson(content);
        }

        /// <summary>
        /// Approves a pending request as the signed-in person, with the app's approval body, and reads the authorization
        /// code from the redirect, checking its <c>state</c>.
        /// </summary>
        /// <param name="authorization">The pending authorization.</param>
        /// <param name="accessToken">An access token from the app's own sign-in.</param>
        /// <param name="approvalBody">The app's approval body, serialized as JSON, or null for none.</param>
        /// <returns>The authorization code.</returns>
        /// <exception cref="OAuthTestFlowException">The approval was refused (403 when the app's handler refused), or the redirect is not the client's with this state.</exception>
        public async Task<string> ApproveAsync(OAuthTestAuthorization authorization, string accessToken, object? approvalBody = null)
        {
            ArgumentNullException.ThrowIfNull(authorization);
            Dictionary<string, string> parameters = await CompleteAsync(authorization, accessToken, "approve", approvalBody);
            if (!parameters.TryGetValue("code", out string? code))
            {
                throw new OAuthTestFlowException("The approval's redirect carries no code.", HttpStatusCode.OK, parameters.GetValueOrDefault("error"), parameters.GetValueOrDefault("error_description"), "");
            }

            return code;
        }

        /// <summary>
        /// Denies a pending request as the signed-in person and returns the <c>error</c> the redirect carries,
        /// <c>access_denied</c>.
        /// </summary>
        /// <param name="authorization">The pending authorization.</param>
        /// <param name="accessToken">An access token from the app's own sign-in.</param>
        /// <returns>The redirect's error code.</returns>
        /// <exception cref="OAuthTestFlowException">The denial was refused, or the redirect is not the client's with this state.</exception>
        public async Task<string?> DenyAsync(OAuthTestAuthorization authorization, string accessToken)
        {
            ArgumentNullException.ThrowIfNull(authorization);
            Dictionary<string, string> parameters = await CompleteAsync(authorization, accessToken, "deny", approvalBody: null);
            return parameters.GetValueOrDefault("error");
        }

        /// <summary>
        /// Redeems an authorization code at <c>/oauth/token</c>.
        /// </summary>
        /// <param name="authorization">The authorization the code came from.</param>
        /// <param name="code">The code.</param>
        /// <param name="codeVerifier">A verifier other than the authorization's, to test the server's refusal of it; null uses the authorization's.</param>
        /// <returns>The tokens.</returns>
        /// <exception cref="OAuthTestFlowException">The server refused the code.</exception>
        public Task<OAuthTestTokens> RedeemCodeAsync(OAuthTestAuthorization authorization, string code, string? codeVerifier = null)
        {
            ArgumentNullException.ThrowIfNull(authorization);
            ArgumentNullException.ThrowIfNull(code);
            List<KeyValuePair<string, string>> form =
            [
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", authorization.RedirectUri),
                new KeyValuePair<string, string>("code_verifier", codeVerifier ?? authorization.CodeVerifier),
            ];
            if (authorization.Resource != null)
            {
                form.Add(new KeyValuePair<string, string>("resource", authorization.Resource));
            }

            return PostTokenAsync(authorization.Client, form);
        }

        /// <summary>
        /// Refreshes at <c>/oauth/token</c>. The refresh token is used up; the result carries the next one.
        /// </summary>
        /// <param name="client">The client the refresh token is bound to.</param>
        /// <param name="refreshToken">The refresh token.</param>
        /// <returns>The new tokens.</returns>
        /// <exception cref="OAuthTestFlowException">The server refused the refresh.</exception>
        public Task<OAuthTestTokens> RefreshAsync(OAuthTestRegisteredClient client, string refreshToken)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(refreshToken);
            return PostTokenAsync(client,
            [
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
            ]);
        }

        /// <summary>
        /// Revokes a token at <c>/oauth/revoke</c> (RFC 7009), which answers 200 whether or not the token was known.
        /// </summary>
        /// <param name="client">The client revoking.</param>
        /// <param name="token">The token.</param>
        /// <returns>A task that completes when the server has answered.</returns>
        /// <exception cref="OAuthTestFlowException">The server did not answer 200.</exception>
        public async Task RevokeAsync(OAuthTestRegisteredClient client, string token)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(token);
            using HttpRequestMessage request = AuthenticatedFormRequest(OAuthServerPaths.Revoke, client, [new KeyValuePair<string, string>("token", token)]);
            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            string content = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw FromBody("The revocation was refused.", response.StatusCode, content);
            }
        }

        /// <summary>
        /// The whole connection in one call: authorize, approve as the signed-in person, redeem the code.
        /// </summary>
        /// <param name="client">The client connecting.</param>
        /// <param name="accessToken">An access token from the app's own sign-in, for the approving person.</param>
        /// <param name="approvalBody">The app's approval body, or null.</param>
        /// <param name="resource">The <c>resource</c> to name, or null.</param>
        /// <returns>The tokens.</returns>
        /// <exception cref="OAuthTestFlowException">A step was refused.</exception>
        public async Task<OAuthTestTokens> ConnectAsync(OAuthTestRegisteredClient client, string accessToken, object? approvalBody = null, string? resource = null)
        {
            OAuthTestAuthorization authorization = await AuthorizeAsync(client, resource);
            string code = await ApproveAsync(authorization, accessToken, approvalBody);
            return await RedeemCodeAsync(authorization, code);
        }

        private async Task<Dictionary<string, string>> CompleteAsync(OAuthTestAuthorization authorization, string accessToken, string action, object? approvalBody)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, RequestPath(authorization) + "/" + action);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            if (approvalBody != null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(approvalBody), Encoding.UTF8, "application/json");
            }

            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            string content = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw FromBody($"The {action} was refused.", response.StatusCode, content);
            }

            string redirectUri = OAuthConsentRedirect.FromJson(content).RedirectUri;
            if (!redirectUri.StartsWith(authorization.RedirectUri, StringComparison.Ordinal))
            {
                throw new OAuthTestFlowException($"The {action} redirects somewhere other than the client's redirect URI.", response.StatusCode, null, null, "");
            }

            Dictionary<string, string> parameters = ParseQuery(redirectUri);
            if (parameters.GetValueOrDefault("state") != authorization.State)
            {
                throw new OAuthTestFlowException($"The {action}'s redirect does not return the request's state.", response.StatusCode, null, null, "");
            }

            if (!parameters.ContainsKey("iss"))
            {
                throw new OAuthTestFlowException($"The {action}'s redirect carries no iss (RFC 9207).", response.StatusCode, null, null, "");
            }

            return parameters;
        }

        private async Task<OAuthTestTokens> PostTokenAsync(OAuthTestRegisteredClient client, List<KeyValuePair<string, string>> form)
        {
            using HttpRequestMessage request = AuthenticatedFormRequest(OAuthServerPaths.Token, client, form);
            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            string content = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw FromBody("The token request was refused.", response.StatusCode, content);
            }

            using JsonDocument document = JsonDocument.Parse(content);
            JsonElement root = document.RootElement;
            return new OAuthTestTokens
            {
                AccessToken = root.GetProperty("access_token").GetString()!,
                TokenType = root.GetProperty("token_type").GetString()!,
                ExpiresIn = root.GetProperty("expires_in").GetInt64(),
                RefreshToken = root.GetProperty("refresh_token").GetString()!,
            };
        }

        private static HttpRequestMessage AuthenticatedFormRequest(string path, OAuthTestRegisteredClient client, List<KeyValuePair<string, string>> form)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, path);
            List<KeyValuePair<string, string>> fields = new List<KeyValuePair<string, string>>(form);
            switch (client.AuthenticationMethod)
            {
                case OAuthClientAuthenticationMethod.ClientSecretBasic:
                    string credentials = Uri.EscapeDataString(client.ClientId) + ":" + Uri.EscapeDataString(client.ClientSecret ?? "");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
                    break;
                case OAuthClientAuthenticationMethod.ClientSecretPost:
                    fields.Add(new KeyValuePair<string, string>("client_id", client.ClientId));
                    fields.Add(new KeyValuePair<string, string>("client_secret", client.ClientSecret ?? ""));
                    break;
                default:
                    fields.Add(new KeyValuePair<string, string>("client_id", client.ClientId));
                    break;
            }

            request.Content = new FormUrlEncodedContent(fields);
            return request;
        }

        private static string RequestPath(OAuthTestAuthorization authorization)
        {
            return OAuthServerPaths.AuthorizationRequestsPrefix + "/" + Uri.EscapeDataString(authorization.RequestId);
        }

        private static bool IsRedirect(HttpStatusCode statusCode)
        {
            return (int)statusCode >= 300 && (int)statusCode < 400;
        }

        /// <summary>
        /// The query parameters of a URL, absolute or relative, each by its last value.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns>The parameters.</returns>
        public static Dictionary<string, string> ParseQuery(string url)
        {
            ArgumentNullException.ThrowIfNull(url);
            int queryStart = url.IndexOf('?');
            Dictionary<string, string> parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            if (queryStart < 0)
            {
                return parameters;
            }

            foreach (KeyValuePair<string, StringValues> parameter in QueryHelpers.ParseQuery(url.Substring(queryStart)))
            {
                parameters[parameter.Key] = parameter.Value.Count == 0 ? "" : parameter.Value[parameter.Value.Count - 1] ?? "";
            }

            return parameters;
        }

        private static OAuthTestFlowException FromBody(string message, HttpStatusCode statusCode, string content)
        {
            string? error = null;
            string? description = null;
            try
            {
                using JsonDocument document = JsonDocument.Parse(content);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    error = document.RootElement.TryGetProperty("error", out JsonElement errorElement) && errorElement.ValueKind == JsonValueKind.String ? errorElement.GetString() : null;
                    description = document.RootElement.TryGetProperty("error_description", out JsonElement descriptionElement) && descriptionElement.ValueKind == JsonValueKind.String
                        ? descriptionElement.GetString()
                        : document.RootElement.TryGetProperty("detail", out JsonElement detailElement) && detailElement.ValueKind == JsonValueKind.String ? detailElement.GetString() : null;
                }
            }
            catch (JsonException)
            {
                // Not JSON: the status and body say what there is to say.
            }

            return new OAuthTestFlowException($"{message} ({(int)statusCode})", statusCode, error, description, content);
        }
    }
}
