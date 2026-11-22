using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// <c>AddOAuthServerEndpoints</c> hands back each endpoint's builder, and metadata added to one builder lands on that
    /// endpoint alone: the method and path its property documents.
    /// </summary>
    [TestClass]
    public class OAuthServerEndpointsTests
    {
        [TestMethod]
        public async Task AddOAuthServerEndpoints_MetadataAddedToEachBuilder_LandsOnItsOwnEndpointOnly()
        {
            WebApplication app = Build(HostedOAuthServer.DefaultOptions());
            OAuthServerEndpoints endpoints = app.AddOAuthServerEndpoints();
            Dictionary<string, RouteHandlerBuilder> byName = new Dictionary<string, RouteHandlerBuilder>
            {
                ["GET " + OAuthServerPaths.AuthorizationServerMetadata] = endpoints.AuthorizationServerMetadata,
                ["GET " + OAuthServerPaths.ProtectedResourceMetadataPrefix + "/mcp"] = endpoints.ProtectedResourceMetadata,
                ["POST " + OAuthServerPaths.Register] = endpoints.Registration!,
                ["GET " + OAuthServerPaths.Authorize] = endpoints.Authorization,
                ["POST " + OAuthServerPaths.Token] = endpoints.Token,
                ["POST " + OAuthServerPaths.Revoke] = endpoints.Revocation,
                ["GET " + OAuthServerPaths.AuthorizationRequestsPrefix + "/{request}"] = endpoints.ConsentRequest,
                ["POST " + OAuthServerPaths.AuthorizationRequestsPrefix + "/{request}/approve"] = endpoints.ConsentApproval,
                ["POST " + OAuthServerPaths.AuthorizationRequestsPrefix + "/{request}/deny"] = endpoints.ConsentDenial,
            };
            foreach (KeyValuePair<string, RouteHandlerBuilder> entry in byName)
            {
                entry.Value.WithMetadata(new EndpointMarker(entry.Key));
            }

            await using (app)
            {
                await app.StartAsync();
                List<RouteEndpoint> mapped = app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

                foreach (string name in byName.Keys)
                {
                    RouteEndpoint marked = mapped.Single(endpoint => endpoint.Metadata.GetOrderedMetadata<EndpointMarker>().Any(marker => marker.Name == name));
                    string method = marked.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single();
                    Assert.AreEqual(name, method + " " + marked.RoutePattern.RawText, "The builder for " + name + " belongs to another endpoint.");
                    Assert.AreEqual(1, marked.Metadata.GetOrderedMetadata<EndpointMarker>().Count, name + "'s endpoint carries another builder's metadata.");
                }
            }
        }

        [TestMethod]
        public async Task AddOAuthServerEndpoints_RegistrationOff_HasNoRegistrationBuilder()
        {
            WebApplication app = Build(HostedOAuthServer.DefaultOptions() with { AllowDynamicClientRegistration = false });

            OAuthServerEndpoints endpoints = app.AddOAuthServerEndpoints();

            await using (app)
            {
                Assert.IsNull(endpoints.Registration);
            }
        }

        [TestMethod]
        public async Task AddOAuthServerEndpoints_AFilterOnTheTokenBuilder_AnswersTheTokenEndpointAlone()
        {
            WebApplication app = Build(HostedOAuthServer.DefaultOptions());
            OAuthServerEndpoints endpoints = app.AddOAuthServerEndpoints();
            endpoints.Token.AddEndpointFilter((context, next) => ValueTask.FromResult<object?>(Results.StatusCode(StatusCodes.Status418ImATeapot)));

            await using (app)
            {
                await app.StartAsync();
                HttpClient client = app.GetTestClient();
                using HttpResponseMessage token = await client.PostAsync(OAuthServerPaths.Token, new FormUrlEncodedContent([]));
                using HttpResponseMessage revoke = await client.PostAsync(OAuthServerPaths.Revoke, new FormUrlEncodedContent([]));

                Assert.AreEqual(418, (int)token.StatusCode);
                Assert.AreNotEqual(418, (int)revoke.StatusCode);
            }
        }

        private static WebApplication Build(OAuthServerOptions options)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddEasyReasyAuth(HostedOAuthServer.Secret);
            builder.Services.AddEasyReasyOAuthServer<InMemoryOAuthServerStore, FakeOAuthGrantHandler>(options);
            WebApplication app = builder.Build();
            app.UseEasyReasyAuth(delay => delay.Enabled = false);
            return app;
        }

        private sealed record EndpointMarker(string Name);
    }
}
