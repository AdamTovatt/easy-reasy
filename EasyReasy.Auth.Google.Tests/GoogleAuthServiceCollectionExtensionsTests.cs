using EasyReasy.Auth.Google;
using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth.Google.Tests
{
    /// <summary>
    /// DI-level tests proving what <see cref="GoogleAuthServiceCollectionExtensions.AddEasyReasyGoogleAuth"/>
    /// registers — and, deliberately, what it leaves to the consumer (the <see cref="IGoogleAuthHandler"/>).
    /// </summary>
    [TestClass]
    public class GoogleAuthServiceCollectionExtensionsTests
    {
        private const string ClientId = "client-id.apps.googleusercontent.com";

        [TestMethod]
        public void AddEasyReasyGoogleAuth_RegistersTokenValidator()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddEasyReasyGoogleAuth(ClientId);

            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.IsNotNull(provider.GetService<IGoogleIdTokenValidator>());
        }

        [TestMethod]
        public void AddEasyReasyGoogleAuth_RegistersGoogleAuthService_WhenHandlerProvided()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddEasyReasyGoogleAuth(ClientId);
            services.AddScoped<IGoogleAuthHandler, FakeGoogleAuthHandler>();

            using ServiceProvider provider = services.BuildServiceProvider();
            using IServiceScope scope = provider.CreateScope();

            Assert.IsNotNull(scope.ServiceProvider.GetService<GoogleAuthService>());
        }

        [TestMethod]
        public void AddEasyReasyGoogleAuth_DoesNotRegisterHandler()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddEasyReasyGoogleAuth(ClientId);

            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.IsNull(provider.GetService<IGoogleAuthHandler>());
        }

        [TestMethod]
        public void AddEasyReasyGoogleAuth_PopulatesOptionsFromArguments()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddEasyReasyGoogleAuth(ClientId, new[] { "example.com" }, requireVerifiedEmail: false);

            using ServiceProvider provider = services.BuildServiceProvider();
            GoogleAuthOptions options = provider.GetRequiredService<GoogleAuthOptions>();

            Assert.AreEqual(ClientId, options.ClientId);
            Assert.IsNotNull(options.AllowedHostedDomains);
            Assert.IsTrue(options.AllowedHostedDomains!.Contains("example.com"));
            Assert.IsFalse(options.RequireVerifiedEmail);
        }

        [TestMethod]
        public void AddEasyReasyGoogleAuth_BlankClientId_Throws()
        {
            ServiceCollection services = new ServiceCollection();

            Assert.ThrowsException<ArgumentException>(() => services.AddEasyReasyGoogleAuth("   "));
        }
    }
}
