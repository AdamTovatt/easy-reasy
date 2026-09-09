using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Integration tests over the in-process <see cref="TestServer"/> seam covering requests that carry no
    /// credentials at all. Such a request never becomes an authentication attempt, so it must be answered with a
    /// 400 validation problem naming the missing wire fields rather than the 401 that means "wrong credentials".
    /// Bodies are posted as raw JSON because the request models cannot express an absent member.
    /// </summary>
    [TestClass]
    public class AuthEndpointMissingCredentialTests
    {
        private const string LoginPath = "/api/auth/login";
        private const string ApiKeyPath = "/api/auth/apikey";
        private const string NoStore = "no-store";

        [TestMethod]
        public async Task LoginEndpoint_WithAbsentUsername_ShouldReturn400NamingUsernameOnly()
        {
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await StartAsync(validationService);

            HttpResponseMessage response = await PostJsonAsync(host.Client, LoginPath, "{\"password\":\"correct-horse\"}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Dictionary<string, string[]> errors = await ReadValidationErrorsAsync(response);
            CollectionAssert.AreEquivalent(new string[] { "username" }, errors.Keys.ToArray());
            Assert.AreEqual(0, validationService.LoginCallCount);
        }

        [TestMethod]
        public async Task LoginEndpoint_WithAbsentPassword_ShouldReturn400NamingPasswordOnly()
        {
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await StartAsync(validationService);

            HttpResponseMessage response = await PostJsonAsync(host.Client, LoginPath, "{\"username\":\"alice\"}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Dictionary<string, string[]> errors = await ReadValidationErrorsAsync(response);
            CollectionAssert.AreEquivalent(new string[] { "password" }, errors.Keys.ToArray());
            Assert.AreEqual(0, validationService.LoginCallCount);
        }

        [TestMethod]
        public async Task LoginEndpoint_WithBothFieldsAbsent_ShouldReturn400NamingBoth()
        {
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await StartAsync(validationService);

            HttpResponseMessage response = await PostJsonAsync(host.Client, LoginPath, "{}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Dictionary<string, string[]> errors = await ReadValidationErrorsAsync(response);
            CollectionAssert.AreEquivalent(new string[] { "username", "password" }, errors.Keys.ToArray());
            Assert.AreEqual(0, validationService.LoginCallCount);
        }

        [TestMethod]
        public async Task LoginEndpoint_WithMissingCredentials_ShouldNameTheMissingFieldInTheMessage()
        {
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await StartAsync(validationService);

            HttpResponseMessage response = await PostJsonAsync(host.Client, LoginPath, "{}");

            Dictionary<string, string[]> errors = await ReadValidationErrorsAsync(response);
            CollectionAssert.AreEqual(new string[] { "The username field is required." }, errors["username"]);
            CollectionAssert.AreEqual(new string[] { "The password field is required." }, errors["password"]);
        }

        [TestMethod]
        public async Task LoginEndpoint_WithEmailInsteadOfUsername_ShouldReturn400NamingUsername()
        {
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await StartAsync(validationService);

            // The exact mistake this endpoint used to answer as a wrong password: the caller spelled the
            // identifier the way its own domain does, and the field the contract publishes stayed unset.
            HttpResponseMessage response = await PostJsonAsync(
                host.Client, LoginPath, "{\"email\":\"alice@example.com\",\"password\":\"correct-horse\"}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Dictionary<string, string[]> errors = await ReadValidationErrorsAsync(response);
            CollectionAssert.AreEquivalent(new string[] { "username" }, errors.Keys.ToArray());
            Assert.AreEqual(0, validationService.LoginCallCount);
        }

        [TestMethod]
        public async Task LoginEndpoint_WithEmptyUsername_ShouldReturn400NamingUsername()
        {
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await StartAsync(validationService);

            HttpResponseMessage response = await PostJsonAsync(host.Client, LoginPath, "{\"username\":\"\",\"password\":\"correct-horse\"}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Dictionary<string, string[]> errors = await ReadValidationErrorsAsync(response);
            CollectionAssert.AreEquivalent(new string[] { "username" }, errors.Keys.ToArray());
            Assert.AreEqual(0, validationService.LoginCallCount);
        }

        [TestMethod]
        public async Task LoginEndpoint_WithEmptyPassword_ShouldReturn400NamingPassword()
        {
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await StartAsync(validationService);

            HttpResponseMessage response = await PostJsonAsync(host.Client, LoginPath, "{\"username\":\"alice\",\"password\":\"\"}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Dictionary<string, string[]> errors = await ReadValidationErrorsAsync(response);
            CollectionAssert.AreEquivalent(new string[] { "password" }, errors.Keys.ToArray());
            Assert.AreEqual(0, validationService.LoginCallCount);
        }

        [TestMethod]
        public async Task LoginEndpoint_WithExplicitJsonNulls_ShouldReturn400NamingBoth()
        {
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await StartAsync(validationService);

            // An explicit JSON null is a different wire input from an absent member, and must land the same way.
            HttpResponseMessage response = await PostJsonAsync(host.Client, LoginPath, "{\"username\":null,\"password\":null}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Dictionary<string, string[]> errors = await ReadValidationErrorsAsync(response);
            CollectionAssert.AreEquivalent(new string[] { "username", "password" }, errors.Keys.ToArray());
            Assert.AreEqual(0, validationService.LoginCallCount);
        }

        [TestMethod]
        public async Task LoginEndpoint_WithWhitespaceOnlyUsername_ShouldStillReturn401WithItsAuditRow()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, validationService);

            // Whitespace is a value the caller supplied, so it goes on to the lookup and takes the ordinary 401.
            HttpResponseMessage response = await PostJsonAsync(host.Client, LoginPath, "{\"username\":\"   \",\"password\":\"correct-horse\"}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(NoStore, response.Headers.CacheControl?.ToString());
            Assert.AreEqual(1, validationService.LoginCallCount);
            Assert.AreEqual(1, auditLogger.LoginCalls.Count);
            Assert.AreEqual(LoginFailureReason.InvalidCredentials, auditLogger.LoginCalls[0].Result.FailureReason);
            Assert.AreEqual("   ", auditLogger.LoginCalls[0].Result.AttemptedSubject);
        }

        [TestMethod]
        public async Task LoginEndpoint_WithWhitespaceOnlyPassword_ShouldStillReturn401()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, validationService);

            HttpResponseMessage response = await PostJsonAsync(host.Client, LoginPath, "{\"username\":\"alice\",\"password\":\"   \"}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(NoStore, response.Headers.CacheControl?.ToString());
            Assert.AreEqual(1, validationService.LoginCallCount);
            Assert.AreEqual(1, auditLogger.LoginCalls.Count);
        }

        [TestMethod]
        public async Task LoginEndpoint_WithAbsentUsername_ShouldInvokeAuditHookOnceWithNullAttemptedSubject()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, new StubValidationService(succeed: false));

            HttpResponseMessage response = await PostJsonAsync(host.Client, LoginPath, "{\"password\":\"correct-horse\"}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(1, auditLogger.LoginCalls.Count);
            Assert.IsFalse(auditLogger.LoginCalls[0].Result.Success);
            Assert.AreEqual(LoginFailureReason.MissingCredentials, auditLogger.LoginCalls[0].Result.FailureReason);
            Assert.IsNull(auditLogger.LoginCalls[0].Result.AttemptedSubject);
        }

        [TestMethod]
        public async Task LoginEndpoint_WithEmptyUsername_ShouldAuditNoSubjectRatherThanAnEmptyOne()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, new StubValidationService(succeed: false));

            // An identifier the endpoint treats as missing is audited as no identifier at all, so a row either
            // names a subject the caller supplied or names none — it never carries an empty string.
            HttpResponseMessage response = await PostJsonAsync(host.Client, LoginPath, "{\"username\":\"\",\"password\":\"\"}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(1, auditLogger.LoginCalls.Count);
            Assert.IsNull(auditLogger.LoginCalls[0].Result.AttemptedSubject);
        }

        [TestMethod]
        public async Task LoginEndpoint_WithAbsentPassword_ShouldInvokeAuditHookOnceWithTheAttemptedSubject()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, new StubValidationService(succeed: false));

            HttpResponseMessage response = await PostJsonAsync(host.Client, LoginPath, "{\"username\":\"alice\"}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(1, auditLogger.LoginCalls.Count);
            Assert.AreEqual(LoginFailureReason.MissingCredentials, auditLogger.LoginCalls[0].Result.FailureReason);
            Assert.AreEqual("alice", auditLogger.LoginCalls[0].Result.AttemptedSubject);
        }

        [TestMethod]
        public async Task LoginEndpoint_WithMissingCredentials_ShouldSetNoStoreOnThe400()
        {
            await using HostedAuthApp host = await StartAsync(new StubValidationService(succeed: false));

            HttpResponseMessage response = await PostJsonAsync(host.Client, LoginPath, "{}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(NoStore, response.Headers.CacheControl?.ToString());
        }

        [TestMethod]
        public async Task LoginEndpoint_WithoutRegisteredAuditLogger_ShouldStillReturn400()
        {
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger: null, validationService);

            HttpResponseMessage response = await PostJsonAsync(host.Client, LoginPath, "{}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(0, validationService.LoginCallCount);
        }

        /// <remarks>
        /// This lives beside the credential-less cases on purpose: it is the other half of the same contract.
        /// What the caller may distinguish is a request that never became an attempt; what it may not
        /// distinguish is one attempt's outcome from another's.
        /// </remarks>
        [TestMethod]
        public async Task LoginEndpoint_UnknownUserAndInvalidCredentials_ShouldAnswerIdentically()
        {
            await using HostedAuthApp unknownUserHost = await StartAsync(
                new StubValidationService(succeed: false, LoginFailureReason.UnknownUser));
            await using HostedAuthApp invalidCredentialsHost = await StartAsync(
                new StubValidationService(succeed: false, LoginFailureReason.InvalidCredentials));

            HttpResponseMessage unknownUserResponse = await PostJsonAsync(
                unknownUserHost.Client, LoginPath, "{\"username\":\"ghost\",\"password\":\"anything\"}");
            HttpResponseMessage invalidCredentialsResponse = await PostJsonAsync(
                invalidCredentialsHost.Client, LoginPath, "{\"username\":\"alice\",\"password\":\"wrong\"}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, unknownUserResponse.StatusCode);
            Assert.AreEqual(invalidCredentialsResponse.StatusCode, unknownUserResponse.StatusCode);
            Assert.AreEqual(
                await invalidCredentialsResponse.Content.ReadAsStringAsync(),
                await unknownUserResponse.Content.ReadAsStringAsync());
            string[] unknownUserHeaders = DistinguishingHeaders(unknownUserResponse);
            CollectionAssert.Contains(unknownUserHeaders, $"Cache-Control: {NoStore}");
            CollectionAssert.AreEqual(DistinguishingHeaders(invalidCredentialsResponse), unknownUserHeaders);
        }

        [TestMethod]
        public async Task ApiKeyEndpoint_WithAbsentApiKey_ShouldReturn400NamingApiKey()
        {
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await StartAsync(validationService);

            HttpResponseMessage response = await PostJsonAsync(host.Client, ApiKeyPath, "{}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Dictionary<string, string[]> errors = await ReadValidationErrorsAsync(response);
            CollectionAssert.AreEquivalent(new string[] { "apiKey" }, errors.Keys.ToArray());
            CollectionAssert.AreEqual(new string[] { "The apiKey field is required." }, errors["apiKey"]);
            Assert.AreEqual(0, validationService.ApiKeyCallCount);
        }

        [TestMethod]
        public async Task ApiKeyEndpoint_WithMisspelledApiKeyField_ShouldReturn400NamingApiKey()
        {
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await StartAsync(validationService);

            HttpResponseMessage response = await PostJsonAsync(host.Client, ApiKeyPath, "{\"key\":\"some-key\"}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Dictionary<string, string[]> errors = await ReadValidationErrorsAsync(response);
            CollectionAssert.AreEquivalent(new string[] { "apiKey" }, errors.Keys.ToArray());
            Assert.AreEqual(0, validationService.ApiKeyCallCount);
        }

        [TestMethod]
        public async Task ApiKeyEndpoint_WithEmptyApiKey_ShouldReturn400NamingApiKey()
        {
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await StartAsync(validationService);

            HttpResponseMessage response = await PostJsonAsync(host.Client, ApiKeyPath, "{\"apiKey\":\"\"}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Dictionary<string, string[]> errors = await ReadValidationErrorsAsync(response);
            CollectionAssert.AreEquivalent(new string[] { "apiKey" }, errors.Keys.ToArray());
            Assert.AreEqual(0, validationService.ApiKeyCallCount);
        }

        [TestMethod]
        public async Task ApiKeyEndpoint_WithWhitespaceOnlyApiKey_ShouldStillReturn401WithItsAuditRow()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, validationService);

            HttpResponseMessage response = await PostJsonAsync(host.Client, ApiKeyPath, "{\"apiKey\":\"   \"}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(NoStore, response.Headers.CacheControl?.ToString());
            Assert.AreEqual(1, validationService.ApiKeyCallCount);
            Assert.AreEqual(1, auditLogger.ApiKeyCalls.Count);
            Assert.AreEqual(ApiKeyAuthFailureReason.UnknownKey, auditLogger.ApiKeyCalls[0].Result.FailureReason);
        }

        [TestMethod]
        public async Task ApiKeyEndpoint_WithAbsentApiKeyAndNoClientId_ShouldInvokeAuditHookOnceWithNullClientId()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, new StubValidationService(succeed: false));

            HttpResponseMessage response = await PostJsonAsync(host.Client, ApiKeyPath, "{}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(1, auditLogger.ApiKeyCalls.Count);
            Assert.IsFalse(auditLogger.ApiKeyCalls[0].Result.Success);
            Assert.AreEqual(ApiKeyAuthFailureReason.MissingKey, auditLogger.ApiKeyCalls[0].Result.FailureReason);
            Assert.IsNull(auditLogger.ApiKeyCalls[0].Result.AttemptedClientId);
        }

        [TestMethod]
        public async Task ApiKeyEndpoint_WithAbsentApiKeyButAClientId_ShouldInvokeAuditHookOnceWithThatClientId()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, new StubValidationService(succeed: false));

            HttpResponseMessage response = await PostJsonAsync(host.Client, ApiKeyPath, "{\"clientId\":\"client-x\"}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(1, auditLogger.ApiKeyCalls.Count);
            Assert.AreEqual(ApiKeyAuthFailureReason.MissingKey, auditLogger.ApiKeyCalls[0].Result.FailureReason);
            Assert.AreEqual("client-x", auditLogger.ApiKeyCalls[0].Result.AttemptedClientId);
        }

        [TestMethod]
        public async Task ApiKeyEndpoint_WithAbsentApiKeyAndAnEmptyClientId_ShouldAuditNoClientIdRatherThanAnEmptyOne()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, new StubValidationService(succeed: false));

            HttpResponseMessage response = await PostJsonAsync(host.Client, ApiKeyPath, "{\"clientId\":\"\"}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(1, auditLogger.ApiKeyCalls.Count);
            Assert.IsNull(auditLogger.ApiKeyCalls[0].Result.AttemptedClientId);
        }

        [TestMethod]
        public async Task ApiKeyEndpoint_WithMissingApiKey_ShouldSetNoStoreOnThe400()
        {
            await using HostedAuthApp host = await StartAsync(new StubValidationService(succeed: false));

            HttpResponseMessage response = await PostJsonAsync(host.Client, ApiKeyPath, "{}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(NoStore, response.Headers.CacheControl?.ToString());
        }

        [TestMethod]
        public async Task ApiKeyEndpoint_WithoutRegisteredAuditLogger_ShouldStillReturn400()
        {
            StubValidationService validationService = new StubValidationService(succeed: false);
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger: null, validationService);

            HttpResponseMessage response = await PostJsonAsync(host.Client, ApiKeyPath, "{}");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(0, validationService.ApiKeyCallCount);
        }

        [TestMethod]
        public async Task CredentialEndpoints_ShouldDeclareTheir200400And401InTheOpenApiDocument()
        {
            await using HostedAuthApp host = await StartAsync(new StubValidationService(succeed: false));

            CollectionAssert.AreEquivalent(new int[] { 200, 400, 401 }, DeclaredStatusCodes(host, LoginPath));
            CollectionAssert.AreEquivalent(new int[] { 200, 400, 401 }, DeclaredStatusCodes(host, ApiKeyPath));
        }

        /// <summary>
        /// Starts a host with an audit logger registered but not observed, for the tests whose subject is the
        /// response rather than the audit row.
        /// </summary>
        private static Task<HostedAuthApp> StartAsync(StubValidationService validationService)
        {
            return HostedAuthApp.StartAsync(new RecordingAuditLogger(), validationService);
        }

        /// <summary>
        /// Posts a raw JSON body, so a test can express a member the request model cannot — an absent one.
        /// </summary>
        private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, string json)
        {
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            return await client.PostAsync(path, content);
        }

        /// <summary>
        /// Reads the <c>errors</c> object of a validation problem response as a map of wire field name to the
        /// messages reported under it.
        /// </summary>
        private static async Task<Dictionary<string, string[]>> ReadValidationErrorsAsync(HttpResponseMessage response)
        {
            string body = await response.Content.ReadAsStringAsync();

            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement errors = document.RootElement.GetProperty("errors");

            Dictionary<string, string[]> messagesByField = new Dictionary<string, string[]>();

            foreach (JsonProperty field in errors.EnumerateObject())
            {
                List<string> messages = new List<string>();

                foreach (JsonElement message in field.Value.EnumerateArray())
                {
                    string? text = message.GetString();

                    if (text != null)
                    {
                        messages.Add(text);
                    }
                }

                messagesByField[field.Name] = messages.ToArray();
            }

            return messagesByField;
        }

        /// <summary>
        /// The response headers that could tell two 401s apart, as "name: value" strings in a stable order.
        /// </summary>
        private static string[] DistinguishingHeaders(HttpResponseMessage response)
        {
            return response.Headers
                .Concat(response.Content.Headers)
                .Select(header => $"{header.Key}: {string.Join(",", header.Value)}")
                .OrderBy(header => header, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// The response status codes an endpoint declares as OpenAPI metadata, which is what a consumer
        /// generating a client from the document sees.
        /// </summary>
        private static int[] DeclaredStatusCodes(HostedAuthApp host, string routePattern)
        {
            EndpointDataSource endpointDataSource = host.App.Services.GetRequiredService<EndpointDataSource>();

            RouteEndpoint endpoint = endpointDataSource.Endpoints
                .OfType<RouteEndpoint>()
                .Single(candidate => candidate.RoutePattern.RawText == routePattern);

            return endpoint.Metadata
                .GetOrderedMetadata<IProducesResponseTypeMetadata>()
                .Select(metadata => metadata.StatusCode)
                .ToArray();
        }
    }
}
