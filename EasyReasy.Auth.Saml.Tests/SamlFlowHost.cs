using EasyReasy.Auth.Tests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// An application with the SAML endpoints on a test server, its in-memory store and dictionary resolver, and the fake
    /// identity provider behind the key <see cref="IdentityProviderKey"/>: drives a sign-in from Begin through the
    /// assertion consumer service to completion, through the endpoints or through <see cref="ISamlAuthService"/>.
    /// </summary>
    internal sealed class SamlFlowHost : IAsyncDisposable
    {
        public const string IdentityProviderKey = "acme";
        public const string JwtSecret = "super_secret_key_12345_12345_12345";

        private SamlFlowHost(
            WebApplication app,
            HttpClient client,
            FakeTimeProvider clock,
            SamlAuthOptions options,
            InMemorySamlAuthStore store,
            FakeSamlIdentityProviderResolver resolver,
            FakeSamlAuthHandler handler)
        {
            App = app;
            Client = client;
            Clock = clock;
            Options = options;
            Store = store;
            Resolver = resolver;
            Handler = handler;
        }

        public WebApplication App { get; }

        public HttpClient Client { get; }

        public FakeTimeProvider Clock { get; }

        public SamlAuthOptions Options { get; }

        public InMemorySamlAuthStore Store { get; }

        public FakeSamlIdentityProviderResolver Resolver { get; }

        public FakeSamlAuthHandler Handler { get; }

        public static FakeSamlIdentityProvider IdentityProvider { get; } = new FakeSamlIdentityProvider("https://idp.example.test/metadata", SamlTestCertificates.IdentityProvider);

        /// <summary>
        /// The account the fake identity provider asserts unless a test names another subject: its default NameID, behind
        /// <see cref="IdentityProviderKey"/>.
        /// </summary>
        public static SamlIdentity DefaultIdentity { get; } = new SamlIdentity(IdentityProviderKey, IdentityProvider.EntityId, FakeSamlIdentityProvider.DefaultNameId);

        public static async Task<SamlFlowHost> StartAsync(
            SamlIdentitySource? identitySource = null,
            X509Certificate2? signingCertificate = null,
            IReadOnlyList<X509Certificate2>? decryptionCertificates = null,
            IAuthAuditLogger? auditLogger = null,
            TimeSpan? sweepInterval = null,
            Action<SamlAuthEndpoints>? configureEndpoints = null,
            ILoggerProvider? loggerProvider = null)
        {
            FakeTimeProvider clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
            SamlAuthOptions options = SamlValidationHarness.CreateOptions(decryptionCertificates ?? [], signingCertificate, sweepInterval);
            InMemorySamlAuthStore store = new InMemorySamlAuthStore();
            FakeSamlIdentityProviderResolver resolver = new FakeSamlIdentityProviderResolver();
            resolver.IdentityProviders[IdentityProviderKey] = new SamlIdentityProvider(IdentityProvider.MetadataXml(), identitySource);
            FakeSamlAuthHandler handler = new FakeSamlAuthHandler
            {
                ResponseToReturn = new AuthResponse(
                    new JwtTokenService(JwtSecret).CreateToken("user-7", "saml", [], [], DateTime.UtcNow.AddMinutes(5)),
                    DateTime.UtcNow.AddMinutes(5).ToString("O")),
            };

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            if (loggerProvider != null)
            {
                builder.Logging.AddProvider(loggerProvider);
            }

            builder.Services.AddSingleton<TimeProvider>(clock);
            builder.Services.AddEasyReasyAuth(JwtSecret);
            builder.Services.AddEasyReasySamlAuth(options);

            builder.Services.AddSingleton<ISamlAuthStore>(store);
            builder.Services.AddSingleton<ISamlIdentityProviderResolver>(resolver);
            builder.Services.AddSingleton<ISamlAuthHandler>(handler);
            if (auditLogger != null)
            {
                builder.Services.AddSingleton(auditLogger);
            }

            WebApplication app = builder.Build();
            SamlAuthEndpoints endpoints = app.AddSamlAuthEndpoints();
            configureEndpoints?.Invoke(endpoints);
            await app.StartAsync();

            TestServer server = (TestServer)app.Services.GetRequiredService<IServer>();
            return new SamlFlowHost(app, server.CreateClient(), clock, options, store, resolver, handler);
        }

        public async Task<HttpResponseMessage> PostBeginAsync(BrowserCookieJar browser, string? identityProviderKey = IdentityProviderKey)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, SamlAuthPaths.Begin)
            {
                Content = JsonContent.Create(new { identityProviderKey }),
            };
            return await SendAsync(request, browser);
        }

        /// <summary>
        /// Begins a sign-in through the begin endpoint and returns the URL it sends the browser to.
        /// </summary>
        public async Task<string> BeginAsync(BrowserCookieJar browser, string identityProviderKey = IdentityProviderKey)
        {
            HttpResponseMessage response = await PostBeginAsync(browser, identityProviderKey);
            Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("redirectUrl").GetString()!;
        }

        /// <summary>
        /// Posts a response to the assertion consumer service as the identity provider's cross-site form post does: with
        /// no cookies.
        /// </summary>
        public async Task<HttpResponseMessage> PostAssertionAsync(string samlResponse)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, SamlAuthPaths.AssertionConsumerService)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["SAMLResponse"] = samlResponse }),
            };
            return await Client.SendAsync(request);
        }

        /// <summary>
        /// Answers the AuthnRequest in <paramref name="redirectUrl"/> with the fake identity provider, posts the answer to
        /// the assertion consumer service, and returns the completion code from its redirect.
        /// </summary>
        public async Task<string> AnswerAsync(string redirectUrl, Action<FakeSamlResponse>? customize = null)
        {
            return await PostAssertionForCodeAsync(Respond(redirectUrl, customize));
        }

        /// <summary>
        /// Posts a response to the assertion consumer service, asserts it redirects to the completion page, and returns
        /// the completion code from the redirect.
        /// </summary>
        public async Task<string> PostAssertionForCodeAsync(string samlResponse)
        {
            HttpResponseMessage response = await PostAssertionAsync(samlResponse);
            Assert.AreEqual(System.Net.HttpStatusCode.SeeOther, response.StatusCode, await response.Content.ReadAsStringAsync());
            return ReadCode(response.Headers.Location!);
        }

        public FakeSamlResponse CreateResponse(string redirectUrl)
        {
            return IdentityProvider.Respond(new SamlRedirectReader(redirectUrl).RequestId, Options);
        }

        public string Respond(string redirectUrl, Action<FakeSamlResponse>? customize = null)
        {
            FakeSamlResponse response = CreateResponse(redirectUrl);
            customize?.Invoke(response);
            return response.ToFormValue();
        }

        public static string ReadCode(Uri location)
        {
            Uri absolute = location.IsAbsoluteUri ? location : new Uri(new Uri("https://app.example.test"), location);
            return QueryHelpers.ParseQuery(absolute.Query)["code"].ToString();
        }

        public async Task<HttpResponseMessage> PostSignInAsync(BrowserCookieJar browser, string code)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, SamlAuthPaths.SignIn)
            {
                Content = JsonContent.Create(new { code }),
            };
            return await SendAsync(request, browser);
        }

        /// <summary>
        /// Begins a sign-in through <see cref="ISamlAuthService"/>, as an application's own endpoint does, with the
        /// browser's cookies; the binding cookie it sets lands in the browser.
        /// </summary>
        public async Task<SamlAuthBeginResult?> ServiceBeginAsync(BrowserCookieJar browser, SamlAuthBeginRequest request)
        {
            await using AsyncServiceScope scope = App.Services.CreateAsyncScope();
            DefaultHttpContext httpContext = CreateHttpContext(browser, scope.ServiceProvider);
            SamlAuthBeginResult? result = await scope.ServiceProvider.GetRequiredService<ISamlAuthService>().BeginAsync(request, httpContext);
            browser.Receive(httpContext.Response.Headers.SetCookie.Select(value => value!));
            return result;
        }

        /// <summary>
        /// Completes a sign-in through <see cref="ISamlAuthService"/> with the browser's cookies; what it clears leaves the
        /// browser.
        /// </summary>
        public async Task<SamlAuthCompletionResult> ServiceCompleteAsync(BrowserCookieJar browser, string code, string expectedCallerBinding = SamlAuthBeginRequestBuilder.DefaultCallerBinding)
        {
            await using AsyncServiceScope scope = App.Services.CreateAsyncScope();
            DefaultHttpContext httpContext = CreateHttpContext(browser, scope.ServiceProvider);
            SamlAuthCompletionResult result = await scope.ServiceProvider.GetRequiredService<ISamlAuthService>().CompleteAsync(code, expectedCallerBinding, httpContext);
            browser.Receive(httpContext.Response.Headers.SetCookie.Select(value => value!));
            return result;
        }

        /// <summary>
        /// Begins a sign-in through <see cref="ISamlAuthService"/> with <see cref="AppRequest"/>, answers it with the fake
        /// identity provider, and returns the completion code, which <paramref name="browser"/> holds the binding cookie for.
        /// </summary>
        public async Task<string> BeginAndAnswerAsync(
            BrowserCookieJar browser,
            Action<SamlAuthBeginRequestBuilder>? customizeRequest = null,
            Action<FakeSamlResponse>? customizeResponse = null)
        {
            return await BeginAndAnswerAsync(browser, AppRequest(customizeRequest), customizeResponse);
        }

        /// <summary>
        /// Begins a sign-in through <see cref="ISamlAuthService"/> with <paramref name="request"/>, answers it with the fake
        /// identity provider, and returns the completion code, which <paramref name="browser"/> holds the binding cookie for.
        /// </summary>
        public async Task<string> BeginAndAnswerAsync(BrowserCookieJar browser, SamlAuthBeginRequest request, Action<FakeSamlResponse>? customizeResponse = null)
        {
            SamlAuthBeginResult? begun = await ServiceBeginAsync(browser, request);
            Assert.IsNotNull(begun, "The request's key resolved to no identity provider.");
            return await AnswerAsync(begun.RedirectUrl, customizeResponse);
        }

        /// <summary>
        /// Runs a whole sign-in through <see cref="ISamlAuthService"/> in a new browser: begins it with
        /// <see cref="AppRequest"/>, answers it with the fake identity provider and completes it.
        /// </summary>
        public async Task<SamlAuthCompletionResult> SignInThroughServiceAsync(
            Action<SamlAuthBeginRequestBuilder>? customizeRequest = null,
            Action<FakeSamlResponse>? customizeResponse = null)
        {
            BrowserCookieJar browser = new BrowserCookieJar();
            SamlAuthBeginRequest request = AppRequest(customizeRequest);
            string code = await BeginAndAnswerAsync(browser, request, customizeResponse);
            return await ServiceCompleteAsync(browser, code, request.CallerBinding);
        }

        /// <summary>
        /// A begin request for the fake identity provider an application's own flow would send.
        /// </summary>
        public static SamlAuthBeginRequest AppRequest(Action<SamlAuthBeginRequestBuilder>? customize = null)
        {
            SamlAuthBeginRequestBuilder builder = new SamlAuthBeginRequestBuilder();
            customize?.Invoke(builder);
            return builder.Build();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }

        private static DefaultHttpContext CreateHttpContext(BrowserCookieJar browser, IServiceProvider services)
        {
            DefaultHttpContext httpContext = new DefaultHttpContext { RequestServices = services };
            if (browser.CookieHeader is string header)
            {
                httpContext.Request.Headers.Cookie = header;
            }

            return httpContext;
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, BrowserCookieJar browser)
        {
            browser.Apply(request);
            HttpResponseMessage response = await Client.SendAsync(request);
            browser.Receive(response);
            return response;
        }
    }
}
