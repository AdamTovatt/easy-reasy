namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class TimeProviderLifetimeValidatorTests
    {
        [TestMethod]
        public void Create_NullTimeProvider_ThrowsArgumentNullException()
        {
            TimeProvider? missing = null;

            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => TimeProviderLifetimeValidator.Create(missing!));

            Assert.AreEqual("timeProvider", exception.ParamName);
        }
    }
}
