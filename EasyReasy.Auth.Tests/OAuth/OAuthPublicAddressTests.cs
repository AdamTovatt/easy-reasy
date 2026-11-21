using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The address guard on metadata document fetches: only public unicast addresses are connected to.
    /// </summary>
    [TestClass]
    public class OAuthPublicAddressTests
    {
        [TestMethod]
        [DataRow("8.8.8.8")]
        [DataRow("1.1.1.1")]
        [DataRow("100.63.255.255")]
        [DataRow("172.32.0.1")]
        [DataRow("2606:4700:4700::1111")]
        [DataRow("::ffff:8.8.8.8")]
        public void IsPublic_PublicAddress_IsTrue(string address)
        {
            Assert.IsTrue(OAuthPublicAddress.IsPublic(IPAddress.Parse(address)));
        }

        [TestMethod]
        [DataRow("0.0.0.0")]
        [DataRow("10.1.2.3")]
        [DataRow("100.64.0.1")]
        [DataRow("100.100.100.100")]
        [DataRow("127.0.0.1")]
        [DataRow("169.254.169.254")]
        [DataRow("172.16.0.1")]
        [DataRow("172.31.255.255")]
        [DataRow("192.0.0.8")]
        [DataRow("192.0.2.1")]
        [DataRow("192.168.1.1")]
        [DataRow("198.18.0.1")]
        [DataRow("198.51.100.7")]
        [DataRow("203.0.113.9")]
        [DataRow("224.0.0.1")]
        [DataRow("255.255.255.255")]
        [DataRow("::")]
        [DataRow("::1")]
        [DataRow("fc00::1")]
        [DataRow("fd12:3456::1")]
        [DataRow("fe80::1")]
        [DataRow("ff02::1")]
        [DataRow("::ffff:127.0.0.1")]
        [DataRow("::ffff:10.0.0.1")]
        [DataRow("64:ff9b::a9fe:a9fe")]
        [DataRow("2002:7f00:1::1")]
        [DataRow("2001:db8::1")]
        [DataRow("2001::1")]
        public void IsPublic_NonPublicAddress_IsFalse(string address)
        {
            Assert.IsFalse(OAuthPublicAddress.IsPublic(IPAddress.Parse(address)));
        }

        [TestMethod]
        public void AddEasyReasyOAuthServer_MetadataDocumentHandler_GuardsEveryConnectionAndUsesNoProxy()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddEasyReasyOAuthServer<InMemoryOAuthServerStore, FakeOAuthGrantHandler>(HostedOAuthServer.DefaultOptions());
            using ServiceProvider provider = services.BuildServiceProvider();

            HttpMessageHandler handler = provider.GetRequiredService<IHttpMessageHandlerFactory>()
                .CreateHandler(OAuthServerServiceCollectionExtensions.MetadataDocumentHttpClientName);
            while (handler is DelegatingHandler delegating)
            {
                handler = delegating.InnerHandler!;
            }

            SocketsHttpHandler primary = (SocketsHttpHandler)handler;
            Assert.IsFalse(primary.UseProxy);
            Assert.IsFalse(primary.AllowAutoRedirect);
            Assert.IsFalse(primary.UseCookies);
            Assert.IsNotNull(primary.ConnectCallback);
        }
    }
}
