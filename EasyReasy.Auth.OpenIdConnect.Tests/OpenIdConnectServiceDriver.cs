using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// Drives the registered <see cref="IOpenIdConnectAuthService"/> of a service provider the way a browser carries a
    /// flow: each call gets a request context from the browser's cookies and hands the response's cookies back to it.
    /// Shared by <see cref="OpenIdConnectAuthHarness"/>, over fake issuers, and the Keycloak tests, over the real network.
    /// </summary>
    internal sealed class OpenIdConnectServiceDriver : IDisposable
    {
        private readonly IServiceScope _scope;

        /// <summary>
        /// Takes ownership of <paramref name="services"/>, and resolves the service once from a scope of its own, as an
        /// application resolves it per request.
        /// </summary>
        public OpenIdConnectServiceDriver(ServiceProvider services)
        {
            Services = services;
            _scope = services.CreateScope();
            Service = _scope.ServiceProvider.GetRequiredService<IOpenIdConnectAuthService>();
        }

        public ServiceProvider Services { get; }

        public IOpenIdConnectAuthService Service { get; }

        /// <summary>
        /// A request context from the browser, with the driver's services as its request services.
        /// </summary>
        public DefaultHttpContext CreateContext(FakeBrowser browser)
        {
            DefaultHttpContext context = new DefaultHttpContext { RequestServices = Services };
            browser.ApplyTo(context.Request);
            return context;
        }

        /// <summary>
        /// Begins a flow whose provider key resolves; <see cref="TryBeginAsync"/> for one that may not.
        /// </summary>
        public async Task<OpenIdConnectAuthBeginResult> BeginAsync(OpenIdConnectAuthBeginRequest request, FakeBrowser browser)
        {
            return await TryBeginAsync(request, browser)
                ?? throw new AssertFailedException($"The provider key '{request.ProviderKey}' resolved to no provider.");
        }

        public async Task<OpenIdConnectAuthBeginResult?> TryBeginAsync(OpenIdConnectAuthBeginRequest request, FakeBrowser browser)
        {
            DefaultHttpContext context = CreateContext(browser);
            try
            {
                return await Service.BeginAsync(request, context);
            }
            finally
            {
                browser.Absorb(context.Response);
            }
        }

        public async Task<OpenIdConnectAuthCompletionResult> CompleteAsync(OpenIdConnectAuthorizationResponse response, FakeBrowser browser, string expectedCallerBinding)
        {
            DefaultHttpContext context = CreateContext(browser);
            OpenIdConnectAuthCompletionResult result = await Service.CompleteAsync(response, expectedCallerBinding, context);
            browser.Absorb(context.Response);
            return result;
        }

        public void Dispose()
        {
            _scope.Dispose();
            Services.Dispose();
        }
    }
}
