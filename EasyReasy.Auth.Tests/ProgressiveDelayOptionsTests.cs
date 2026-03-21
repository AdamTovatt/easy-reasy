namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class ProgressiveDelayOptionsTests
    {
        [TestMethod]
        public void DefaultValues_ShouldBeCorrect()
        {
            ProgressiveDelayOptions options = new ProgressiveDelayOptions();

            Assert.IsTrue(options.Enabled);
            Assert.AreEqual(0, options.TrustedProxyCount);
            Assert.AreEqual(TimeSpan.FromMilliseconds(500), options.DelayIncrement);
            Assert.AreEqual(10, options.FreeFailures);
            Assert.AreEqual(TimeSpan.FromSeconds(30), options.MaxDelay);
            Assert.AreEqual(TimeSpan.FromHours(1), options.FailureEntryLifetime);
        }

        [TestMethod]
        public void Validate_WithNegativeTrustedProxyCount_ShouldThrow()
        {
            ProgressiveDelayOptions options = new ProgressiveDelayOptions { TrustedProxyCount = -1 };

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new ProgressiveDelayMiddleware(next: _ => Task.CompletedTask, options: options));
        }

        [TestMethod]
        public void Validate_WithNegativeFreeFailures_ShouldThrow()
        {
            ProgressiveDelayOptions options = new ProgressiveDelayOptions { FreeFailures = -1 };

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new ProgressiveDelayMiddleware(next: _ => Task.CompletedTask, options: options));
        }

        [TestMethod]
        public void Validate_WithNegativeDelayIncrement_ShouldThrow()
        {
            ProgressiveDelayOptions options = new ProgressiveDelayOptions { DelayIncrement = TimeSpan.FromMilliseconds(-1) };

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new ProgressiveDelayMiddleware(next: _ => Task.CompletedTask, options: options));
        }

        [TestMethod]
        public void Validate_WithExcessiveDelayIncrement_ShouldThrow()
        {
            ProgressiveDelayOptions options = new ProgressiveDelayOptions { DelayIncrement = TimeSpan.FromDays(365) };

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new ProgressiveDelayMiddleware(next: _ => Task.CompletedTask, options: options));
        }

        [TestMethod]
        public void Validate_WithNegativeMaxDelay_ShouldThrow()
        {
            ProgressiveDelayOptions options = new ProgressiveDelayOptions { MaxDelay = TimeSpan.FromMilliseconds(-1) };

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new ProgressiveDelayMiddleware(next: _ => Task.CompletedTask, options: options));
        }

        [TestMethod]
        public void Validate_WithExcessiveMaxDelay_ShouldThrow()
        {
            ProgressiveDelayOptions options = new ProgressiveDelayOptions { MaxDelay = TimeSpan.FromDays(365) };

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new ProgressiveDelayMiddleware(next: _ => Task.CompletedTask, options: options));
        }

        [TestMethod]
        public void Validate_WithNegativeFailureEntryLifetime_ShouldThrow()
        {
            ProgressiveDelayOptions options = new ProgressiveDelayOptions { FailureEntryLifetime = TimeSpan.FromMilliseconds(-1) };

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new ProgressiveDelayMiddleware(next: _ => Task.CompletedTask, options: options));
        }
    }
}
