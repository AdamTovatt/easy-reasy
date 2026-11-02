using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class EasyReasyAuthOptionsTests
    {
        private const string ValidSecret = "super_secret_key_12345_12345_12345";

        [TestMethod]
        public void DefaultValues_ShouldBeCorrect()
        {
            EasyReasyAuthOptions options = new EasyReasyAuthOptions();

            Assert.IsNull(options.Issuer);
            Assert.IsNull(options.Audience);
            Assert.AreEqual(TimeSpan.FromSeconds(30), options.ClockSkew);
            Assert.IsTrue(options.RegisterJwtTokenService);
            Assert.AreEqual(0, options.QueryStringTokenPaths.Count);
            Assert.AreEqual(0, options.CookieTokenPaths.Count);
        }

        [TestMethod]
        [DataRow("files")]
        [DataRow("/files/")]
        [DataRow("/")]
        [DataRow("")]
        public void AddEasyReasyAuth_WithCookieTokenPathThatNeverMatches_ShouldThrow(string path)
        {
            ServiceCollection services = new ServiceCollection();

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() =>
                services.AddEasyReasyAuth(ValidSecret, options =>
                {
                    options.CookieTokenPaths = ["/media", path];
                }));
            Assert.AreEqual(nameof(EasyReasyAuthOptions.CookieTokenPaths), exception.ParamName);
        }

        [TestMethod]
        [DataRow("/files", "/files/private")]
        [DataRow("/files/private", "/files")]
        [DataRow("/files", "/FILES/private")]
        [DataRow("/files", "/files")]
        [DataRow("/files", "/Files")]
        public void AddEasyReasyAuth_WithNestedCookieTokenPaths_ShouldThrow(string first, string second)
        {
            ServiceCollection services = new ServiceCollection();

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() =>
                services.AddEasyReasyAuth(ValidSecret, options =>
                {
                    options.CookieTokenPaths = [first, "/media", second];
                }));
            Assert.AreEqual(nameof(EasyReasyAuthOptions.CookieTokenPaths), exception.ParamName);
        }

        [TestMethod]
        public void AddEasyReasyAuth_WithSiblingCookieTokenPaths_ShouldNotThrow()
        {
            ServiceCollection services = new ServiceCollection();

            // "/filesx" shares only a string prefix with "/files", not a segment, so neither is inside the other.
            services.AddEasyReasyAuth(ValidSecret, options =>
            {
                options.CookieTokenPaths = ["/files", "/filesx", "/media/thumbnails"];
            });
        }

        [TestMethod]
        public void AddEasyReasyAuth_WithSamePathInBothTokenPathLists_ShouldNotThrow()
        {
            ServiceCollection services = new ServiceCollection();

            // The nesting rule is about two cookies; a query-string path never writes one.
            services.AddEasyReasyAuth(ValidSecret, options =>
            {
                options.QueryStringTokenPaths = ["/files"];
                options.CookieTokenPaths = ["/files"];
            });
        }

        [TestMethod]
        [DataRow("ws")]
        [DataRow("/ws/")]
        [DataRow("/")]
        [DataRow("")]
        public void AddEasyReasyAuth_WithQueryStringTokenPathThatNeverMatches_ShouldThrow(string path)
        {
            ServiceCollection services = new ServiceCollection();

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() =>
                services.AddEasyReasyAuth(ValidSecret, options =>
                {
                    options.QueryStringTokenPaths = ["/api", path];
                }));
            Assert.AreEqual(nameof(EasyReasyAuthOptions.QueryStringTokenPaths), exception.ParamName);
        }

        [TestMethod]
        public void AddEasyReasyAuth_WithValidQueryStringTokenPaths_ShouldNotThrow()
        {
            ServiceCollection services = new ServiceCollection();

            services.AddEasyReasyAuth(ValidSecret, options =>
            {
                options.QueryStringTokenPaths = ["/ws", "/api/live/feed"];
            });
        }

        [TestMethod]
        public void AddEasyReasyAuth_WithNegativeClockSkew_ShouldThrow()
        {
            ServiceCollection services = new ServiceCollection();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                services.AddEasyReasyAuth(ValidSecret, options =>
                {
                    options.ClockSkew = TimeSpan.FromSeconds(-1);
                }));
        }

        [TestMethod]
        public void AddEasyReasyAuth_WithEmptyIssuer_ShouldThrow()
        {
            ServiceCollection services = new ServiceCollection();

            Assert.ThrowsException<ArgumentException>(() =>
                services.AddEasyReasyAuth(ValidSecret, options =>
                {
                    options.Issuer = "";
                }));
        }

        [TestMethod]
        public void AddEasyReasyAuth_WithWhitespaceIssuer_ShouldThrow()
        {
            ServiceCollection services = new ServiceCollection();

            Assert.ThrowsException<ArgumentException>(() =>
                services.AddEasyReasyAuth(ValidSecret, options =>
                {
                    options.Issuer = "   ";
                }));
        }

        [TestMethod]
        public void AddEasyReasyAuth_WithEmptyAudience_ShouldThrow()
        {
            ServiceCollection services = new ServiceCollection();

            Assert.ThrowsException<ArgumentException>(() =>
                services.AddEasyReasyAuth(ValidSecret, options =>
                {
                    options.Audience = "";
                }));
        }

        [TestMethod]
        public void AddEasyReasyAuth_WithWhitespaceAudience_ShouldThrow()
        {
            ServiceCollection services = new ServiceCollection();

            Assert.ThrowsException<ArgumentException>(() =>
                services.AddEasyReasyAuth(ValidSecret, options =>
                {
                    options.Audience = "   ";
                }));
        }

        [TestMethod]
        public void AddEasyReasyAuth_WithNullIssuer_ShouldNotThrow()
        {
            ServiceCollection services = new ServiceCollection();

            services.AddEasyReasyAuth(ValidSecret, options =>
            {
                options.Issuer = null;
            });
        }

        [TestMethod]
        public void AddEasyReasyAuth_WithNullAudience_ShouldNotThrow()
        {
            ServiceCollection services = new ServiceCollection();

            services.AddEasyReasyAuth(ValidSecret, options =>
            {
                options.Audience = null;
            });
        }
    }
}
