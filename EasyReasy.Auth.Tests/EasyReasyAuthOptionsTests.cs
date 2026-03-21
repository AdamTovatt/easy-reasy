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
    }
}
