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
