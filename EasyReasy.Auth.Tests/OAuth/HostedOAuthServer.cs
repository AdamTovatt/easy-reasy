using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A real <see cref="WebApplication"/> serving the OAuth authorization server over a <see cref="TestServer"/>, with an
    /// in-memory store, a fake grant handler, a recording audit logger, a settable clock and a fake internet for metadata
    /// documents. It also serves the first-party refresh endpoint, <c>/api/me</c>, a protected endpoint open to connections
    /// that echoes the caller's claims, so a test can use the tokens it gets, and five more: one protected endpoint not
    /// opened to connections, one opened by the attribute, one in a route group opened as a whole, one opened but
    /// requiring the admin role, and one opened with no authorization of its own; and
    /// <see cref="OpenedConnectionsController"/>, a controller opened as a whole. <see cref="HostedOAuthServerSetup"/>
    /// changes how EasyReasy.Auth is registered and wired.
    /// </summary>
    internal sealed class HostedOAuthServer : IAsyncDisposable
    {
        public const string Issuer = "https://app.example";
        public const string Resource = "https://app.example/mcp";
        public const string ConsentPageUrl = "/connect/approve";
        public const string MePath = "/api/me";
        public const string FirstPartyOnlyPath = "/api/account";
        public const string AttributeOpenedPath = "/api/opened-by-attribute";
        public const string OpenedGroupPath = "/api/opened-group";
        public const string DefaultRedirectUri = "https://client.example/callback";
        public const string Secret = "super_secret_key_12345_12345_12345";
        public const string JwtIssuer = "test-issuer";
        public const string Audience = "test-api";
        public const string OpenedAdminPath = "/api/opened-admin";
        public const string OpenedWithoutPolicyPath = "/api/opened-without-policy";
        public const string AppBearerChallengeError = "app_sign_in_required";
        public const string AppBearerForbiddenError = "app_forbidden";

        private HostedOAuthServer(WebApplication app, HttpClient client, Parts parts)
        {
            App = app;
            Client = client;
            OAuth = new OAuthTestClient(client);
            Store = parts.Store;
            Interceptor = parts.Interceptor;
            Handler = parts.Handler;
            AuditLogger = parts.AuditLogger;
            Clock = parts.Clock;
            MetadataDocuments = parts.MetadataDocuments;
            FirstPartyStore = parts.FirstPartyStore;
            FirstPartyTokens = parts.FirstPartyTokens;
        }

        public WebApplication App { get; }

        public HttpClient Client { get; }

        public OAuthTestClient OAuth { get; }

        public InMemoryOAuthServerStore Store { get; }

        public InterceptingOAuthServerStore Interceptor { get; }

        public FakeOAuthGrantHandler Handler { get; }

        public RecordingOAuthAuditLogger AuditLogger { get; }

        public FakeTimeProvider Clock { get; }

        public FakeMetadataDocumentHandler MetadataDocuments { get; }

        public FakeRefreshTokenStore FirstPartyStore { get; }

        /// <summary>
        /// Issues the app's own tokens with the setup's audience, whether or not the app registered an
        /// <see cref="IJwtTokenService"/>.
        /// </summary>
        public JwtTokenService FirstPartyTokens { get; }

        public static OAuthServerOptions DefaultOptions()
        {
            return new OAuthServerOptions
            {
                IssuerUrl = Issuer,
                Resource = Resource,
                ConsentPageUrl = ConsentPageUrl,
            };
        }

        public static async Task<HostedOAuthServer> StartAsync(OAuthServerOptions? options = null, bool realMetadataDocumentFetch = false, HostedOAuthServerSetup? setup = null)
        {
            setup ??= new HostedOAuthServerSetup();
            Parts parts = new Parts(setup);
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();

            builder.Services.AddSingleton<TimeProvider>(parts.Clock);
            if (setup.OAuthServerRegisteredFirst)
            {
                builder.Services.AddEasyReasyOAuthServer<InMemoryOAuthServerStore, FakeOAuthGrantHandler>(options ?? DefaultOptions());
            }

            builder.Services.AddEasyReasyAuth(Secret, authOptions =>
            {
                authOptions.Issuer = JwtIssuer;
                authOptions.Audience = setup.Audience;
                authOptions.RegisterJwtTokenService = setup.RegisterJwtTokenService;
            });
            if (!setup.OAuthServerRegisteredFirst)
            {
                builder.Services.AddEasyReasyOAuthServer<InMemoryOAuthServerStore, FakeOAuthGrantHandler>(options ?? DefaultOptions());
            }

            // The registrations above are the app's types; these instances replace them so a test can see inside.
            builder.Services.AddSingleton<IOAuthServerStore>(parts.Interceptor);
            builder.Services.AddSingleton<IOAuthGrantHandler>(parts.Handler);
            builder.Services.AddSingleton<IAuthAuditLogger>(parts.AuditLogger);
            if (!realMetadataDocumentFetch)
            {
                builder.Services.AddHttpClient(OAuthServerServiceCollectionExtensions.MetadataDocumentHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => parts.MetadataDocuments);
            }

            if (setup.AppBearerWritesItsOwnBody)
            {
                builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, bearerOptions => bearerOptions.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new { error = AppBearerChallengeError });
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new { error = AppBearerForbiddenError });
                    },
                });
            }

            builder.Services.AddControllers().AddApplicationPart(typeof(HostedOAuthServer).Assembly);
            builder.Services.AddSingleton<IRefreshTokenStore>(parts.FirstPartyStore);
            builder.Services.AddSingleton<IRefreshTokenService>(provider => new RefreshTokenService(
                provider.GetRequiredService<IRefreshTokenStore>(),
                auditLogger: provider.GetService<IAuthAuditLogger>()));

            WebApplication app = builder.Build();
            switch (setup.Pipeline)
            {
                case HostedOAuthServerPipeline.UseEasyReasyAuth:
                    app.UseEasyReasyAuth(delay => delay.Enabled = false);
                    break;
                case HostedOAuthServerPipeline.AuthenticationAndAuthorizationByHand:
                    app.UseAuthentication();
                    app.UseAuthorization();
                    break;
                case HostedOAuthServerPipeline.RoutingAfterAuthentication:
                    app.UseAuthentication();
                    app.UseRouting();
                    app.UseAuthorization();
                    break;
            }

            app.AddOAuthServerEndpoints();
            if (setup.RegisterJwtTokenService)
            {
                app.AddRefreshEndpoint();   // it issues through the registered IJwtTokenService
            }

            app.MapGet(MePath, (HttpContext httpContext) => Results.Json(new Dictionary<string, object?>
            {
                ["sub"] = httpContext.GetUserId(),
                ["client_id"] = httpContext.GetOAuthClientId(),
                ["organization_id"] = httpContext.GetClaimValue("organization_id"),
                ["family_id"] = httpContext.GetRefreshFamilyId(),
                ["roles"] = httpContext.GetRoles().ToArray(),
                ["client_id_claims"] = httpContext.User.Claims.Count(claim => claim.Type == EasyReasyClaimTypes.ClientId),
            })).RequireAuthorization().AllowOAuthClients();
            app.MapGet(FirstPartyOnlyPath, () => Results.Ok()).RequireAuthorization();
            app.MapGet(AttributeOpenedPath, [AllowOAuthClients] () => Results.Ok()).RequireAuthorization();
            app.MapGroup(OpenedGroupPath).AllowOAuthClients().MapGet("/projects", () => Results.Ok()).RequireAuthorization();
            app.MapGet(OpenedAdminPath, () => Results.Ok()).RequireAuthorization(policy => policy.RequireRole("admin")).AllowOAuthClients();
            app.MapGet(OpenedWithoutPolicyPath, () => Results.Ok()).AllowOAuthClients();
            app.MapControllers();

            await app.StartAsync();
            TestServer testServer = (TestServer)app.Services.GetRequiredService<IServer>();
            HttpClient client = testServer.CreateClient();
            client.BaseAddress = new Uri(Issuer);
            return new HostedOAuthServer(app, client, parts);
        }

        /// <summary>
        /// The app's own sign-in: a first-party access token for a person, as the consent page would hold.
        /// </summary>
        public string SignIn(string subject = "person-1", string authType = "user")
        {
            return FirstPartyTokens.CreateToken(subject, authType, Array.Empty<Claim>(), ["member"], Clock.GetUtcNow().UtcDateTime.AddMinutes(5));
        }

        /// <summary>
        /// Revokes a grant through the app-facing <see cref="IOAuthGrantService"/>, as the app's "disconnect" would.
        /// </summary>
        public async Task RevokeGrantAsync(string grantId)
        {
            using IServiceScope scope = App.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IOAuthGrantService>().RevokeGrantAsync(grantId);
        }

        /// <summary>
        /// Posts a hand-built form, for the requests <see cref="OAuthTestClient"/> never sends.
        /// </summary>
        public async Task<HttpResponseMessage> PostFormAsync(string path, IEnumerable<KeyValuePair<string, string>> fields, string? authorization = null)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(fields) };
            if (authorization != null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", authorization);
            }

            return await Client.SendAsync(request);
        }

        /// <summary>
        /// Posts to a path with a bearer token and no body.
        /// </summary>
        public async Task<HttpResponseMessage> PostAsync(string path, string accessToken)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return await Client.SendAsync(request);
        }

        /// <summary>
        /// The consent page's approve path for a pending request.
        /// </summary>
        public static string ApprovePath(OAuthTestAuthorization authorization)
        {
            return OAuthServerPaths.AuthorizationRequestsPrefix + "/" + authorization.RequestId + "/approve";
        }

        /// <summary>
        /// The signing settings the host's EasyReasy.Auth registered, for a test that signs a token as the server would.
        /// </summary>
        public JwtSigningSettings Signing => App.Services.GetRequiredService<JwtSigningSettings>();

        /// <summary>
        /// Calls <c>/api/me</c> with a bearer token.
        /// </summary>
        public Task<HttpResponseMessage> GetMeAsync(string accessToken)
        {
            return GetAsync(MePath, accessToken);
        }

        /// <summary>
        /// Calls a path with a bearer token.
        /// </summary>
        public async Task<HttpResponseMessage> GetAsync(string path, string accessToken)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return await Client.SendAsync(request);
        }

        /// <summary>
        /// Registers a public client, authorizes it and approves as person-1, stopping at the code: the setup of a test of
        /// what the token endpoint does with it. <paramref name="redirectUri"/> names another redirect URI on the
        /// authorization request than the registered one, as a loopback client's on another port.
        /// </summary>
        public async Task<ApprovedClient> ApprovePublicClientAsync(string registeredRedirectUri = DefaultRedirectUri, string? resource = null, string? redirectUri = null)
        {
            OAuthTestRegisteredClient client = await OAuth.RegisterAsync(registeredRedirectUri);
            OAuthTestAuthorization authorization = await OAuth.AuthorizeAsync(client, resource, redirectUri: redirectUri);
            string code = await OAuth.ApproveAsync(authorization, SignIn());
            return new ApprovedClient(client, authorization, code);
        }

        /// <summary>
        /// Registers a public client and approves a connection for it as person-1, returning everything a test needs next.
        /// </summary>
        public async Task<ConnectedClient> ConnectPublicClientAsync(string redirectUri = DefaultRedirectUri)
        {
            OAuthTestRegisteredClient client = await OAuth.RegisterAsync(redirectUri, clientName: "Test Agent");
            OAuthTestAuthorization authorization = await OAuth.AuthorizeAsync(client, Resource);
            string code = await OAuth.ApproveAsync(authorization, SignIn(), new { organization = "org-7" });
            OAuthTestTokens tokens = await OAuth.RedeemCodeAsync(authorization, code);
            return new ConnectedClient(client, authorization, code, tokens);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                Client.Dispose();
            }
            finally
            {
                await App.DisposeAsync();
            }
        }

        internal sealed record ApprovedClient(OAuthTestRegisteredClient Client, OAuthTestAuthorization Authorization, string Code);

        internal sealed record ConnectedClient(OAuthTestRegisteredClient Client, OAuthTestAuthorization Authorization, string Code, OAuthTestTokens Tokens);

        private sealed class Parts
        {
            public Parts(HostedOAuthServerSetup setup)
            {
                Interceptor = new InterceptingOAuthServerStore(Store);
                FirstPartyTokens = new JwtTokenService(Secret, JwtIssuer, setup.Audience, Clock);
            }

            public JwtTokenService FirstPartyTokens { get; }

            public InMemoryOAuthServerStore Store { get; } = new InMemoryOAuthServerStore();

            public InterceptingOAuthServerStore Interceptor { get; }

            public FakeOAuthGrantHandler Handler { get; } = new FakeOAuthGrantHandler();

            public RecordingOAuthAuditLogger AuditLogger { get; } = new RecordingOAuthAuditLogger();

            public FakeTimeProvider Clock { get; } = new FakeTimeProvider(DateTimeOffset.UtcNow);

            public FakeMetadataDocumentHandler MetadataDocuments { get; } = new FakeMetadataDocumentHandler();

            public FakeRefreshTokenStore FirstPartyStore { get; } = new FakeRefreshTokenStore();
        }
    }
}
