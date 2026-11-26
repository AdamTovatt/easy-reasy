using EasyReasy.Auth.Tests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// The registered <see cref="IOpenIdConnectAuthService"/> wired to three fake issuers on a <see cref="FakeNetwork"/>
    /// (<see cref="PrimaryKey"/>, <see cref="OtherKey"/> and the multi-tenant <see cref="TenantsKey"/>), a
    /// <see cref="FakeProviderResolver"/> and a fake clock, with helpers that run Begin and Complete the way a browser
    /// would carry them.
    /// </summary>
    internal sealed class OpenIdConnectAuthHarness : IDisposable
    {
        public const string PrimaryKey = "primary";
        public const string OtherKey = "other";
        public const string TenantsKey = "tenants";
        public const string CallerBinding = "user-7:record-9";

        private readonly OpenIdConnectServiceDriver _driver;

        private OpenIdConnectAuthHarness(
            ServiceProvider services,
            FakeTimeProvider clock,
            FakeNetwork network,
            FakeProviderResolver resolver,
            FakeOpenIdConnectIssuer primary,
            FakeOpenIdConnectIssuer other,
            FakeOpenIdConnectIssuer tenants)
        {
            _driver = new OpenIdConnectServiceDriver(services);
            Clock = clock;
            Network = network;
            Resolver = resolver;
            Primary = primary;
            Other = other;
            Tenants = tenants;
        }

        public FakeTimeProvider Clock { get; }

        public FakeNetwork Network { get; }

        public FakeProviderResolver Resolver { get; }

        public IOpenIdConnectAuthService Service => _driver.Service;

        public FakeOpenIdConnectIssuer Primary { get; }

        public FakeOpenIdConnectIssuer Other { get; }

        public FakeOpenIdConnectIssuer Tenants { get; }

        public static byte[] StateKey { get; } = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

        public static DateTimeOffset StartTime { get; } = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);

        public static string SignInRedirectUri(string providerKey) => $"https://app.example/auth/{providerKey}/callback";

        public static string SigningRedirectUri(string providerKey) => $"https://app.example/sign/{providerKey}/callback";

        public static OpenIdConnectAuthOptions CreateOptions(byte[]? stateKey = null, TimeSpan? stateLifetime = null)
        {
            return new OpenIdConnectAuthOptions
            {
                StateKey = stateKey ?? StateKey,
                StateLifetime = stateLifetime ?? TimeSpan.FromMinutes(10),
            };
        }

        public static OpenIdConnectAuthHarness Create(OpenIdConnectAuthOptions? options = null)
        {
            FakeTimeProvider clock = new FakeTimeProvider(StartTime);
            FakeNetwork network = new FakeNetwork();
            FakeProviderResolver resolver = new FakeProviderResolver();

            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<TimeProvider>(clock);
            services.AddEasyReasyOpenIdConnectAuth(options ?? CreateOptions());
            services.AddSingleton<IOpenIdConnectProviderResolver>(resolver);
            ConfigureNetwork(services, network);

            FakeOpenIdConnectIssuer primary = new FakeOpenIdConnectIssuer(clock, "primary.fake.test");
            FakeOpenIdConnectIssuer other = new FakeOpenIdConnectIssuer(clock, "other.fake.test");
            FakeOpenIdConnectIssuer tenants = new FakeOpenIdConnectIssuer(clock, "tenants.fake.test", multiTenant: true);
            network.Add(primary);
            network.Add(other);
            network.Add(tenants);

            resolver.Providers[PrimaryKey] = CreateProvider(primary, PrimaryKey);
            resolver.Providers[OtherKey] = CreateProvider(other, OtherKey);
            resolver.Providers[TenantsKey] = CreateProvider(tenants, TenantsKey);

            return new OpenIdConnectAuthHarness(services.BuildServiceProvider(), clock, network, resolver, primary, other, tenants);
        }

        /// <summary>
        /// Points the package's named HTTP client at the fake network.
        /// </summary>
        public static void ConfigureNetwork(IServiceCollection services, FakeNetwork network)
        {
            services.AddHttpClient(OpenIdConnectCodeFlow.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => network);
        }

        /// <summary>
        /// The provider an application would configure for an issuer under a key: its own sign-in and signing redirect
        /// URIs, and for a multi-tenant issuer the template with its tenant claim and metadata address.
        /// </summary>
        public static OpenIdConnectProvider CreateProvider(FakeOpenIdConnectIssuer issuer, string providerKey, IReadOnlyList<string>? signingAlgorithms = null)
        {
            return new OpenIdConnectProvider(
                CreateIssuer(issuer),
                FakeOpenIdConnectIssuer.ClientId,
                FakeOpenIdConnectIssuer.ClientSecret,
                [SignInRedirectUri(providerKey), SigningRedirectUri(providerKey)],
                issuer.ClientAuthentication,
                signingAlgorithms);
        }

        /// <summary>
        /// A copy of a provider with the given values changed and every other value, the client authentication included,
        /// kept.
        /// </summary>
        public static OpenIdConnectProvider CopyProvider(
            OpenIdConnectProvider provider,
            OpenIdConnectIssuer? issuer = null,
            string? clientId = null,
            string? clientSecret = null,
            IReadOnlyList<string>? signingAlgorithms = null,
            string? scope = null)
        {
            return new OpenIdConnectProvider(
                issuer ?? provider.Issuer,
                clientId ?? provider.ClientId,
                clientSecret ?? provider.ClientSecret,
                provider.RedirectUris,
                provider.ClientAuthentication,
                signingAlgorithms ?? provider.SigningAlgorithms,
                scope ?? provider.Scope);
        }

        /// <summary>
        /// Replaces the provider under a key with a copy changed as <see cref="CopyProvider"/> does.
        /// </summary>
        public void ChangeProvider(
            string providerKey,
            OpenIdConnectIssuer? issuer = null,
            string? clientId = null,
            string? clientSecret = null,
            IReadOnlyList<string>? signingAlgorithms = null,
            string? scope = null)
        {
            Resolver.Providers[providerKey] = CopyProvider(Resolver.Providers[providerKey], issuer, clientId, clientSecret, signingAlgorithms, scope);
        }

        public static OpenIdConnectIssuer CreateIssuer(FakeOpenIdConnectIssuer issuer)
        {
            return issuer.IsMultiTenant
                ? OpenIdConnectIssuer.TenantTemplate(issuer.Issuer, "tid", issuer.MetadataAddress)
                : OpenIdConnectIssuer.Fixed(issuer.Issuer);
        }

        public static OpenIdConnectAuthBeginRequest CreateRequest(
            string providerKey = PrimaryKey,
            string? redirectUri = null,
            OpenIdConnectPrompt? prompt = null,
            string? nonce = null,
            string? loginHint = null,
            TimeSpan? maxAuthenticationAge = null,
            IReadOnlyList<string>? acceptedAcrValues = null,
            IReadOnlyList<string>? acceptedAmrValues = null,
            OpenIdConnectIdentity? expectedIdentity = null,
            IReadOnlyDictionary<string, string>? requiredClaimValues = null)
        {
            return new OpenIdConnectAuthBeginRequest
            {
                ProviderKey = providerKey,
                RedirectUri = redirectUri ?? SigningRedirectUri(providerKey),
                CallerBinding = CallerBinding,
                Prompt = prompt,
                Nonce = nonce,
                LoginHint = loginHint,
                MaxAuthenticationAge = maxAuthenticationAge,
                AcceptedAcrValues = acceptedAcrValues,
                AcceptedAmrValues = acceptedAmrValues,
                ExpectedIdentity = expectedIdentity,
                RequiredClaimValues = requiredClaimValues,
            };
        }

        /// <summary>
        /// A flow of its own over the same network and clock, for what only a provider package reaches.
        /// </summary>
        public OpenIdConnectCodeFlow CreateFlow(OpenIdConnectAuthOptions? options = null)
        {
            return new OpenIdConnectCodeFlow(options ?? CreateOptions(), _driver.Services.GetRequiredService<IHttpClientFactory>(), Clock);
        }

        public FakeOpenIdConnectIssuer IssuerFor(string providerKey)
        {
            return providerKey switch
            {
                PrimaryKey => Primary,
                OtherKey => Other,
                TenantsKey => Tenants,
                _ => throw new ArgumentOutOfRangeException(nameof(providerKey), providerKey, "No fake issuer has this key."),
            };
        }

        /// <summary>
        /// A new browser whose cookies expire on this harness's clock.
        /// </summary>
        public FakeBrowser CreateBrowser()
        {
            return new FakeBrowser(Clock);
        }

        public DefaultHttpContext CreateContext(FakeBrowser browser) => _driver.CreateContext(browser);

        public Task<OpenIdConnectAuthBeginResult> BeginAsync(OpenIdConnectAuthBeginRequest request, FakeBrowser browser) => _driver.BeginAsync(request, browser);

        public Task<OpenIdConnectAuthBeginResult?> TryBeginAsync(OpenIdConnectAuthBeginRequest request, FakeBrowser browser) => _driver.TryBeginAsync(request, browser);

        public Task<OpenIdConnectAuthCompletionResult> CompleteAsync(OpenIdConnectAuthorizationResponse response, FakeBrowser browser, string expectedCallerBinding = CallerBinding)
            => _driver.CompleteAsync(response, browser, expectedCallerBinding);

        /// <summary>
        /// Lets the provider of a begun flow sign the person in, and completes the flow in the same browser at the
        /// request's redirect URI (the signing one unless given), with the given <c>iss</c> parameter.
        /// </summary>
        public Task<OpenIdConnectAuthCompletionResult> CompleteBegunAsync(
            OpenIdConnectAuthBeginResult begun,
            FakeBrowser browser,
            string providerKey = PrimaryKey,
            string? redirectUri = null,
            string? responseIssuer = null)
        {
            string code = IssuerFor(providerKey).IssueCode(begun.AuthorizationUrl);
            return CompleteAsync(CreateResponse(code, begun.State, redirectUri ?? SigningRedirectUri(providerKey), responseIssuer), browser);
        }

        /// <summary>
        /// The query parameters of an authorize URL.
        /// </summary>
        public static Dictionary<string, StringValues> ParseQuery(string url)
        {
            return QueryHelpers.ParseQuery(new Uri(url).Query);
        }

        /// <summary>
        /// How many of the flow's binding cookies a browser holds.
        /// </summary>
        public static int CountBindingCookies(FakeBrowser browser)
        {
            return browser.Cookies.Keys.Count(name => name.StartsWith(OpenIdConnectCodeFlow.BindingCookieNamePrefix, StringComparison.Ordinal));
        }

        /// <summary>
        /// The response a page at the flow's redirect URI posts: the code, the state, the issuer's <c>iss</c> when it
        /// sends one, and its own address.
        /// </summary>
        public static OpenIdConnectAuthorizationResponse CreateResponse(string code, string state, string redirectUri, string? issuer = null)
        {
            return new OpenIdConnectAuthorizationResponse(code, state, issuer, redirectUri);
        }

        /// <summary>
        /// Runs a whole flow in one browser: Begin, the person signing in at the request's provider, and completion at the
        /// request's redirect URI, with the <c>iss</c> parameter when the provider advertises it.
        /// </summary>
        public async Task<OpenIdConnectAuthCompletionResult> SignInAsync(OpenIdConnectAuthBeginRequest request, Action<FakeIdTokenSettings>? customizeToken = null)
        {
            FakeOpenIdConnectIssuer issuer = IssuerFor(request.ProviderKey);
            FakeBrowser browser = CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await BeginAsync(request, browser);
            string code = issuer.IssueCode(begun.AuthorizationUrl, customizeToken);
            string? responseIssuer = issuer.AdvertisesIssuerParameter ? issuer.TokenIssuer : null;
            return await CompleteAsync(CreateResponse(code, begun.State, request.RedirectUri, responseIssuer), browser);
        }

        public void Dispose()
        {
            _driver.Dispose();
        }
    }
}
