using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Integration tests over the in-process <see cref="TestServer"/> seam covering what the credential
    /// endpoints publish about themselves: the responses they declare in the OpenAPI document, and the
    /// outcomes they deliberately keep indistinguishable from one another.
    /// </summary>
    [TestClass]
    public class AuthEndpointContractTests
    {
        private const string LoginPath = "/api/auth/login";
        private const string ApiKeyPath = "/api/auth/apikey";

        [TestMethod]
        public async Task CredentialEndpoints_ShouldDeclareTheir200400And401InTheOpenApiDocument()
        {
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(
                new RecordingAuditLogger(), new StubValidationService(succeed: false));

            CollectionAssert.AreEquivalent(new int[] { 200, 400, 401 }, DeclaredStatusCodes(host, LoginPath));
            CollectionAssert.AreEquivalent(new int[] { 200, 400, 401 }, DeclaredStatusCodes(host, ApiKeyPath));
        }

        [TestMethod]
        public async Task LoginEndpoint_UnknownUserAndInvalidCredentials_ShouldAnswerIdentically()
        {
            // The contract this change deliberately did not touch: what a caller may distinguish is a request
            // that never became an attempt, never one attempt's outcome from another's.
            await using HostedAuthApp unknownUserHost = await HostedAuthApp.StartAsync(
                new RecordingAuditLogger(), new StubValidationService(succeed: false, LoginFailureReason.UnknownUser));
            await using HostedAuthApp invalidCredentialsHost = await HostedAuthApp.StartAsync(
                new RecordingAuditLogger(), new StubValidationService(succeed: false, LoginFailureReason.InvalidCredentials));

            HttpResponseMessage unknownUserResponse = await PostJsonAsync(
                unknownUserHost.Client, LoginPath, "{\"username\":\"ghost\",\"password\":\"anything\"}");
            HttpResponseMessage invalidCredentialsResponse = await PostJsonAsync(
                invalidCredentialsHost.Client, LoginPath, "{\"username\":\"alice\",\"password\":\"wrong\"}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, unknownUserResponse.StatusCode);
            Assert.AreEqual(invalidCredentialsResponse.StatusCode, unknownUserResponse.StatusCode);
            Assert.AreEqual(
                await invalidCredentialsResponse.Content.ReadAsStringAsync(),
                await unknownUserResponse.Content.ReadAsStringAsync());

            // The headers a caller could actually read the outcome off, rather than every header the host
            // happens to emit — a whole-header comparison would also fail on anything unrelated the two
            // hosts differ in.
            CollectionAssert.AreEqual(
                OutcomeBearingHeaders(invalidCredentialsResponse),
                OutcomeBearingHeaders(unknownUserResponse));
        }

        /// <summary>
        /// Posts a raw JSON body.
        /// </summary>
        private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, string json)
        {
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            return await client.PostAsync(path, content);
        }

        /// <summary>
        /// The response headers that could tell one authentication outcome from another, as "name: value"
        /// strings in a stable order.
        /// </summary>
        private static string[] OutcomeBearingHeaders(HttpResponseMessage response)
        {
            string[] headerNames = new string[] { "Cache-Control", "WWW-Authenticate", "Content-Length", "Content-Type" };

            return headerNames
                .Select(name => $"{name}: {ReadHeader(response, name)}")
                .ToArray();
        }

        /// <summary>
        /// Reads one header's joined value from either the response or its content, or "&lt;absent&gt;".
        /// </summary>
        private static string ReadHeader(HttpResponseMessage response, string name)
        {
            if (response.Headers.TryGetValues(name, out IEnumerable<string>? responseValues))
            {
                return string.Join(",", responseValues);
            }

            if (response.Content.Headers.TryGetValues(name, out IEnumerable<string>? contentValues))
            {
                return string.Join(",", contentValues);
            }

            return "<absent>";
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
