using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The server's own sweep deletes, through the store, what it can never accept again: expired requests, codes and
    /// refresh tokens, and the clients registered longer than <see cref="OAuthServerOptions.UnusedClientLifetime"/> ago
    /// that nothing live names, measured on the server's clock. A client that registered a moment ago survives. The test
    /// clock moves only the rows' time; the sweep's timer runs on real time, so a test of the timer sets a short interval.
    /// </summary>
    [TestClass]
    public class OAuthServerSweepTests
    {
        private static readonly TimeSpan ShortInterval = TimeSpan.FromMilliseconds(20);

        [TestMethod]
        public async Task AddEasyReasyOAuthServer_Registered_AddsTheSweepAsAHostedService()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();

            Assert.AreEqual(1, server.App.Services.GetServices<IHostedService>().OfType<OAuthServerSweep>().Count());
        }

        [TestMethod]
        public async Task SweepOnceAsync_ClientUnusedForTheRefreshTokenLifetime_IsDeletedAndAFreshOneKept()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient old = await server.OAuth.RegisterAsync(HostedOAuthServer.DefaultRedirectUri);
            server.Clock.Advance(HostedOAuthServer.DefaultOptions().RefreshTokenLifetime + TimeSpan.FromMinutes(1));
            OAuthTestRegisteredClient fresh = await server.OAuth.RegisterAsync(HostedOAuthServer.DefaultRedirectUri);

            await SweepAsync(server);

            CollectionAssert.AreEqual(new[] { fresh.ClientId }, server.Store.Clients.Select(client => client.ClientId).ToArray());
            Assert.AreNotEqual(old.ClientId, fresh.ClientId);
        }

        [TestMethod]
        public async Task SweepOnceAsync_ClientUnusedForJustUnderTheRefreshTokenLifetime_IsKept()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            await server.OAuth.RegisterAsync(HostedOAuthServer.DefaultRedirectUri);
            server.Clock.Advance(HostedOAuthServer.DefaultOptions().RefreshTokenLifetime - TimeSpan.FromMinutes(1));

            await SweepAsync(server);

            Assert.AreEqual(1, server.Store.Clients.Count);
        }

        [TestMethod]
        public async Task SweepOnceAsync_UnusedClientLifetimeSet_DeletesAClientRegisteredThatLongAgo()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(HostedOAuthServer.DefaultOptions() with { UnusedClientLifetime = TimeSpan.FromDays(1) });
            await server.OAuth.RegisterAsync(HostedOAuthServer.DefaultRedirectUri);
            server.Clock.Advance(TimeSpan.FromDays(2));

            await SweepAsync(server);

            Assert.AreEqual(0, server.Store.Clients.Count);
        }

        [TestMethod]
        public async Task SweepOnceAsync_ConnectedClient_KeepsItAndItsLiveRefreshTokenButNotItsExpiredRows()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            await server.OAuth.AuthorizeAsync(connected.Client, HostedOAuthServer.Resource);
            // Past the request's lifetime, and so the shorter code's, but well inside the refresh token's.
            server.Clock.Advance(HostedOAuthServer.DefaultOptions().AuthorizationRequestLifetime + TimeSpan.FromMinutes(1));

            await SweepAsync(server);

            Assert.AreEqual(0, server.Store.AuthorizationRequests.Count);
            Assert.AreEqual(0, server.Store.AuthorizationCodes.Count);
            Assert.AreEqual(1, server.Store.RefreshTokens.Count);
            Assert.AreEqual(connected.Client.ClientId, server.Store.Clients.Single().ClientId);
        }

        [TestMethod]
        public async Task SweepOnceAsync_StoreDependsOnAServiceDisposableOnlyAsynchronously_DisposesItsScope()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddScoped<AsyncOnlyDisposable>();
            services.AddScoped<IOAuthServerStore>(provider =>
            {
                provider.GetRequiredService<AsyncOnlyDisposable>();
                return new InMemoryOAuthServerStore();
            });
            await using ServiceProvider provider = services.BuildServiceProvider();
            OAuthServerSweep sweep = new OAuthServerSweep(
                provider.GetRequiredService<IServiceScopeFactory>(),
                OAuthServerSettings.Create(HostedOAuthServer.DefaultOptions()),
                TimeProvider.System,
                NullLogger<OAuthServerSweep>.Instance);

            await sweep.SweepOnceAsync(CancellationToken.None);

            Assert.AreEqual(1, AsyncOnlyDisposable.Disposed);
        }

        [TestMethod]
        public async Task ExecuteAsync_IntervalPasses_SweepsWithoutACall()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(HostedOAuthServer.DefaultOptions() with { SweepInterval = ShortInterval });
            await server.OAuth.RegisterAsync(HostedOAuthServer.DefaultRedirectUri);
            server.Clock.Advance(HostedOAuthServer.DefaultOptions().RefreshTokenLifetime + TimeSpan.FromMinutes(1));

            await WaitUntilAsync(() => server.Store.Clients.Count == 0, "the timer's sweep to delete the old client");
        }

        [TestMethod]
        [DataRow(typeof(InvalidOperationException), DisplayName = "a database error")]
        [DataRow(typeof(TaskCanceledException), DisplayName = "a store's timeout, surfacing as a cancellation")]
        public async Task ExecuteAsync_StoreFails_LogsTheErrorAndSweepsAgainNextInterval(Type failureType)
        {
            RecordingLoggerProvider logs = new RecordingLoggerProvider();
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(
                HostedOAuthServer.DefaultOptions() with { SweepInterval = ShortInterval },
                setup: new HostedOAuthServerSetup { LoggerProvider = logs });
            server.Interceptor.FailNextDeleteExpired((Exception)Activator.CreateInstance(failureType)!);

            await WaitUntilAsync(() => SweepErrors(logs).Any(), "the failed sweep to be logged");
            int sweepsBefore = server.Interceptor.DeleteExpiredCalls;
            await WaitUntilAsync(() => server.Interceptor.DeleteExpiredCalls > sweepsBefore, "the next interval's sweep");

            Assert.AreEqual(1, SweepErrors(logs).Count());
        }

        [TestMethod]
        public async Task ExecuteAsync_HostStopsWhileWaiting_EndsWithoutAnError()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthServerSweep sweep = HostedSweep(server);

            await server.App.StopAsync();

            Assert.IsNotNull(sweep.ExecuteTask);
            Assert.IsTrue(sweep.ExecuteTask.IsCompletedSuccessfully, $"The sweep ended {sweep.ExecuteTask.Status}.");
        }

        [TestMethod]
        public async Task ExecuteAsync_SweepIntervalAtItsLongest_StartsTheTimer()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(HostedOAuthServer.DefaultOptions() with { SweepInterval = OAuthServerSettings.MaxSweepInterval });

            Assert.IsNotNull(HostedSweep(server).ExecuteTask);
            Assert.IsFalse(HostedSweep(server).ExecuteTask!.IsCompleted);
        }

        private static OAuthServerSweep HostedSweep(HostedOAuthServer server)
        {
            return server.App.Services.GetServices<IHostedService>().OfType<OAuthServerSweep>().Single();
        }

        private static async Task SweepAsync(HostedOAuthServer server)
        {
            await HostedSweep(server).SweepOnceAsync(CancellationToken.None);
        }

        private static IEnumerable<RecordingLoggerProvider.Entry> SweepErrors(RecordingLoggerProvider logs)
        {
            return logs.Entries.Where(entry => entry.Category == typeof(OAuthServerSweep).FullName && entry.Level == LogLevel.Error);
        }

        /// <summary>
        /// A scoped service that implements only <see cref="IAsyncDisposable"/>, as some database connections do: a scope
        /// disposed synchronously throws on it.
        /// </summary>
        private sealed class AsyncOnlyDisposable : IAsyncDisposable
        {
            private static int _disposed;

            public static int Disposed => Volatile.Read(ref _disposed);

            public ValueTask DisposeAsync()
            {
                Interlocked.Increment(ref _disposed);
                return ValueTask.CompletedTask;
            }
        }

        private static async Task WaitUntilAsync(Func<bool> condition, string awaited)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    Assert.Fail($"Timed out waiting for {awaited}.");
                }

                await Task.Delay(10);
            }
        }
    }
}
