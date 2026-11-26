using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    [TestClass]
    public class OpenIdConnectAuthServiceCollectionExtensionsTests
    {
        [TestMethod]
        public void AddEasyReasyOpenIdConnectAuth_ValidOptions_RegistersTheServiceAndTheSystemClock()
        {
            ServiceCollection services = new ServiceCollection();

            services.AddEasyReasyOpenIdConnectAuth(OpenIdConnectAuthHarness.CreateOptions());
            services.AddScoped<IOpenIdConnectProviderResolver, FakeProviderResolver>();
            using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
            using IServiceScope scope = provider.CreateScope();

            Assert.IsNotNull(scope.ServiceProvider.GetRequiredService<IOpenIdConnectAuthService>());
            Assert.AreSame(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
        }

        [TestMethod]
        public void AddEasyReasyOpenIdConnectAuth_NoResolverRegistered_ResolvingTheServiceSaysHowToRegisterOne()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddEasyReasyOpenIdConnectAuth(OpenIdConnectAuthHarness.CreateOptions());
            using ServiceProvider provider = services.BuildServiceProvider();
            using IServiceScope scope = provider.CreateScope();

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IOpenIdConnectAuthService>());

            StringAssert.Contains(exception.Message, nameof(IOpenIdConnectProviderResolver));
        }

        [TestMethod]
        public void AddEasyReasyOpenIdConnectAuth_ClockAlreadyRegistered_KeepsIt()
        {
            ServiceCollection services = new ServiceCollection();
            TimeProvider clock = new EasyReasy.Auth.Tests.FakeTimeProvider(OpenIdConnectAuthHarness.StartTime);
            services.AddSingleton(clock);

            services.AddEasyReasyOpenIdConnectAuth(OpenIdConnectAuthHarness.CreateOptions());
            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.AreSame(clock, provider.GetRequiredService<TimeProvider>());
        }

        [TestMethod]
        public async Task AddEasyReasyOpenIdConnectAuth_StateKeyChangedAfterRegistration_StillOpensStatesSealedBefore()
        {
            byte[] stateKey = OpenIdConnectAuthHarness.StateKey.ToArray();
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create(OpenIdConnectAuthHarness.CreateOptions(stateKey: stateKey));
            Array.Fill(stateKey, (byte)0);

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest());

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public void AddEasyReasyOpenIdConnectAuth_SeveralProblems_ReportsEveryOneInOneException()
        {
            ServiceCollection services = new ServiceCollection();

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => services.AddEasyReasyOpenIdConnectAuth(
                OpenIdConnectAuthHarness.CreateOptions(stateKey: new byte[16], stateLifetime: TimeSpan.Zero)));

            StringAssert.Contains(exception.Message, nameof(OpenIdConnectAuthOptions.StateKey));
            StringAssert.Contains(exception.Message, nameof(OpenIdConnectAuthOptions.StateLifetime));
        }

        [TestMethod]
        public void OpenIdConnectCodeFlow_InvalidOptions_ThrowsAtConstruction()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            Assert.ThrowsException<ArgumentException>(() => harness.CreateFlow(OpenIdConnectAuthHarness.CreateOptions(stateKey: new byte[31])));
        }
    }
}
