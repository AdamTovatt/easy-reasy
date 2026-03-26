using EasyReasy.Metrics.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EasyReasy.Metrics.Tests.Configuration
{
    [TestClass]
    public class EasyReasyMetricsOptionsTests
    {
        [TestMethod]
        public void Validate_WithDefaultOptions_DoesNotThrow()
        {
            EasyReasyMetricsOptions options = new EasyReasyMetricsOptions();

            options.Validate();
        }

        [TestMethod]
        public void Validate_WithZeroCollectionInterval_ThrowsArgumentOutOfRangeException()
        {
            EasyReasyMetricsOptions options = new EasyReasyMetricsOptions
            {
                CollectionInterval = TimeSpan.Zero
            };

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [TestMethod]
        public void Validate_WithNegativeCollectionInterval_ThrowsArgumentOutOfRangeException()
        {
            EasyReasyMetricsOptions options = new EasyReasyMetricsOptions
            {
                CollectionInterval = TimeSpan.FromSeconds(-1)
            };

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [TestMethod]
        public void Validate_WithZeroMinimumTimeBetweenCollections_ThrowsArgumentOutOfRangeException()
        {
            EasyReasyMetricsOptions options = new EasyReasyMetricsOptions
            {
                MinimumTimeBetweenCollections = TimeSpan.Zero
            };

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [TestMethod]
        public void Validate_WithNegativeInitialDelay_ThrowsArgumentOutOfRangeException()
        {
            EasyReasyMetricsOptions options = new EasyReasyMetricsOptions
            {
                InitialDelay = TimeSpan.FromSeconds(-1)
            };

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [TestMethod]
        public void Validate_WithZeroInitialDelay_DoesNotThrow()
        {
            EasyReasyMetricsOptions options = new EasyReasyMetricsOptions
            {
                InitialDelay = TimeSpan.Zero
            };

            options.Validate();
        }
    }
}
