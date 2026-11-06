using EasyReasy.Auth.Tests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth.Microsoft.Tests
{
    /// <summary>
    /// The registered <see cref="IMicrosoftAuthService"/> wired to a <see cref="FakeMicrosoftIdentityProvider"/> and a
    /// fake clock, with helpers that run Begin and Complete the way a browser would carry them.
    /// </summary>
    internal sealed class MicrosoftAuthHarness : IDisposable
    {
        public const string SignInRedirectUri = "https://app.example/auth/microsoft/callback";
        public const string SigningRedirectUri = "https://app.example/sign/microsoft/callback";
        public const string CallerBinding = "user-7:record-9";

        private readonly ServiceProvider _services;

        private MicrosoftAuthHarness(ServiceProvider services, FakeTimeProvider clock, FakeMicrosoftIdentityProvider identityProvider)
        {
            _services = services;
            Clock = clock;
            IdentityProvider = identityProvider;
            Service = services.GetRequiredService<IMicrosoftAuthService>();
        }

        public FakeTimeProvider Clock { get; }

        public FakeMicrosoftIdentityProvider IdentityProvider { get; }

        public IMicrosoftAuthService Service { get; }

        public static byte[] StateKey { get; } = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

        public static DateTimeOffset StartTime { get; } = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// Options for the fake identity provider; each parameter replaces one valid default.
        /// </summary>
        public static MicrosoftAuthOptions CreateOptions(
            string clientId = FakeMicrosoftIdentityProvider.ClientId,
            string clientSecret = FakeMicrosoftIdentityProvider.ClientSecret,
            byte[]? stateKey = null,
            IReadOnlyList<string>? redirectUris = null,
            IReadOnlyCollection<Guid>? allowedTenantIds = null,
            string authorityBaseUrl = FakeMicrosoftIdentityProvider.Authority,
            TimeSpan? stateLifetime = null)
        {
            return new MicrosoftAuthOptions
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                StateKey = stateKey ?? StateKey,
                RedirectUris = redirectUris ?? [SignInRedirectUri, SigningRedirectUri],
                AllowedTenantIds = allowedTenantIds,
                AuthorityBaseUrl = authorityBaseUrl,
                StateLifetime = stateLifetime ?? TimeSpan.FromMinutes(10),
            };
        }

        public static MicrosoftAuthHarness Create(MicrosoftAuthOptions? options = null)
        {
            FakeTimeProvider clock = new FakeTimeProvider(StartTime);
            FakeMicrosoftIdentityProvider identityProvider = new FakeMicrosoftIdentityProvider(clock);

            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<TimeProvider>(clock);
            services.AddEasyReasyMicrosoftAuth(options ?? CreateOptions());
            ConfigureFakeIdentityProvider(services, identityProvider);

            return new MicrosoftAuthHarness(services.BuildServiceProvider(), clock, identityProvider);
        }

        /// <summary>
        /// Points the package's named HTTP client at the fake identity provider.
        /// </summary>
        public static void ConfigureFakeIdentityProvider(IServiceCollection services, FakeMicrosoftIdentityProvider identityProvider)
        {
            services.AddHttpClient(MicrosoftAuthServiceCollectionExtensions.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => identityProvider);
        }

        public static MicrosoftAuthBeginRequest CreateRequest(
            Guid? tenantId = null,
            string? requiredAuthenticationContextId = null,
            TimeSpan? maxAuthenticationAge = null,
            string? nonce = null,
            MicrosoftAuthPrompt? prompt = null,
            string? loginHint = null,
            string redirectUri = SigningRedirectUri)
        {
            return new MicrosoftAuthBeginRequest
            {
                RedirectUri = redirectUri,
                CallerBinding = CallerBinding,
                TenantId = tenantId,
                RequiredAuthenticationContextId = requiredAuthenticationContextId,
                MaxAuthenticationAge = maxAuthenticationAge,
                Nonce = nonce,
                Prompt = prompt,
                LoginHint = loginHint,
            };
        }

        /// <summary>
        /// A new browser whose cookies expire on this harness's clock.
        /// </summary>
        public FakeBrowser CreateBrowser()
        {
            return new FakeBrowser(Clock);
        }

        public MicrosoftAuthBeginResult Begin(MicrosoftAuthBeginRequest request, FakeBrowser browser)
        {
            DefaultHttpContext context = new DefaultHttpContext();
            browser.ApplyTo(context.Request);
            MicrosoftAuthBeginResult result = Service.Begin(request, context);
            browser.Absorb(context.Response);
            return result;
        }

        public async Task<MicrosoftAuthCompletionResult> CompleteAsync(string code, string state, FakeBrowser browser)
        {
            DefaultHttpContext context = new DefaultHttpContext();
            browser.ApplyTo(context.Request);
            MicrosoftAuthCompletionResult result = await Service.CompleteAsync(code, state, context);
            browser.Absorb(context.Response);
            return result;
        }

        /// <summary>
        /// Runs a whole flow in one browser: Begin, the user signing in at the fake identity provider, and Complete.
        /// </summary>
        public async Task<MicrosoftAuthCompletionResult> SignInAsync(MicrosoftAuthBeginRequest request, Action<FakeIdTokenSettings>? customizeToken = null)
        {
            FakeBrowser browser = CreateBrowser();
            MicrosoftAuthBeginResult begun = Begin(request, browser);
            string code = IdentityProvider.IssueCode(begun.AuthorizationUrl, customizeToken);
            return await CompleteAsync(code, begun.State, browser);
        }

        public void Dispose()
        {
            _services.Dispose();
        }
    }
}
