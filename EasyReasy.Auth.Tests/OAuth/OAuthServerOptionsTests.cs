using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The options are checked at registration, every problem in one exception, and the endpoints refuse to map without
    /// what they need.
    /// </summary>
    [TestClass]
    public class OAuthServerOptionsTests
    {
        [TestMethod]
        public void AddEasyReasyOAuthServer_InvalidOptions_ReportsEveryProblem()
        {
            OAuthServerOptions options = new OAuthServerOptions
            {
                IssuerUrl = "https://app.example/tenant",
                Resource = "https://other.example/mcp",
                ConsentPageUrl = "//evil.example/consent",
                ScopesSupported = ["has space"],
                AccessTokenLifetime = TimeSpan.Zero,
                AuthorizationCodeLifetime = TimeSpan.FromSeconds(-1),
            };

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new ServiceCollection().AddEasyReasyOAuthServer<InMemoryOAuthServerStore, FakeOAuthGrantHandler>(options));

            StringAssert.Contains(exception.Message, nameof(OAuthServerOptions.IssuerUrl));
            StringAssert.Contains(exception.Message, nameof(OAuthServerOptions.ConsentPageUrl));
            StringAssert.Contains(exception.Message, nameof(OAuthServerOptions.ScopesSupported));
            StringAssert.Contains(exception.Message, nameof(OAuthServerOptions.AccessTokenLifetime));
            StringAssert.Contains(exception.Message, nameof(OAuthServerOptions.AuthorizationCodeLifetime));
        }

        [TestMethod]
        [DataRow("http://app.example", DisplayName = "http off loopback")]
        [DataRow("https://app.example?x=1", DisplayName = "query")]
        [DataRow("https://app.example#x", DisplayName = "fragment")]
        [DataRow("app.example", DisplayName = "not absolute")]
        public void AddEasyReasyOAuthServer_BadIssuer_Throws(string issuer)
        {
            OAuthServerOptions options = new OAuthServerOptions { IssuerUrl = issuer, Resource = "https://app.example/mcp", ConsentPageUrl = "/consent" };

            Assert.ThrowsException<ArgumentException>(() => new ServiceCollection().AddEasyReasyOAuthServer<InMemoryOAuthServerStore, FakeOAuthGrantHandler>(options));
        }

        [TestMethod]
        public void AddEasyReasyOAuthServer_ResourceOnAnotherOrigin_Throws()
        {
            OAuthServerOptions options = new OAuthServerOptions { IssuerUrl = "https://app.example", Resource = "https://api.example/mcp", ConsentPageUrl = "/consent" };

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new ServiceCollection().AddEasyReasyOAuthServer<InMemoryOAuthServerStore, FakeOAuthGrantHandler>(options));

            StringAssert.Contains(exception.Message, "same origin");
        }

        [TestMethod]
        public void AddEasyReasyOAuthServer_LoopbackHttp_IsAccepted()
        {
            OAuthServerOptions options = new OAuthServerOptions { IssuerUrl = "http://localhost:5000", Resource = "http://localhost:5000/mcp", ConsentPageUrl = "http://localhost:5173/consent" };

            new ServiceCollection().AddEasyReasyOAuthServer<InMemoryOAuthServerStore, FakeOAuthGrantHandler>(options);
        }

        [TestMethod]
        public void AddOAuthServerEndpoints_ServerNotRegistered_ThrowsAtStartup()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            WebApplication app = builder.Build();

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => app.AddOAuthServerEndpoints());

            StringAssert.Contains(exception.Message, "AddEasyReasyOAuthServer");
            StringAssert.Contains(exception.Message, "AddEasyReasyAuth");
        }
    }
}
