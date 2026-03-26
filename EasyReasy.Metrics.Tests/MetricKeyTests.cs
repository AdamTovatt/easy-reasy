using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EasyReasy.Metrics.Tests
{
    [TestClass]
    public class MetricKeyTests
    {
        [TestMethod]
        public void Constructor_WithValidKey_SetsKeyProperty()
        {
            MetricKey metricKey = new MetricKey("total_customers");

            Assert.AreEqual("total_customers", metricKey.Key);
        }

        [TestMethod]
        public void Constructor_WithNullKey_ThrowsArgumentException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new MetricKey(null!));
        }

        [TestMethod]
        public void Constructor_WithEmptyKey_ThrowsArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(() => new MetricKey(""));
        }

        [TestMethod]
        public void Constructor_WithWhitespaceKey_ThrowsArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(() => new MetricKey("   "));
        }

        [TestMethod]
        public void ToString_ReturnsKeyString()
        {
            MetricKey metricKey = new MetricKey("active_policies");

            string result = metricKey.ToString();

            Assert.AreEqual("active_policies", result);
        }

        [TestMethod]
        public void Equals_SameKey_ReturnsTrue()
        {
            MetricKey first = new MetricKey("total_customers");
            MetricKey second = new MetricKey("total_customers");

            bool result = first.Equals(second);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Equals_DifferentKey_ReturnsFalse()
        {
            MetricKey first = new MetricKey("total_customers");
            MetricKey second = new MetricKey("active_policies");

            bool result = first.Equals(second);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void GetHashCode_SameKey_ReturnsSameHash()
        {
            MetricKey first = new MetricKey("total_customers");
            MetricKey second = new MetricKey("total_customers");

            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        }

        [TestMethod]
        public void OperatorEquals_SameKey_ReturnsTrue()
        {
            MetricKey first = new MetricKey("total_customers");
            MetricKey second = new MetricKey("total_customers");

            bool result = first == second;

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void OperatorNotEquals_DifferentKey_ReturnsTrue()
        {
            MetricKey first = new MetricKey("total_customers");
            MetricKey second = new MetricKey("active_policies");

            bool result = first != second;

            Assert.IsTrue(result);
        }
    }
}
