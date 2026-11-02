using System.Net;

namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// The public refusal rule, <see cref="AuthorizedHttpClient.IsRefusalStatus"/>, at its boundaries. Reads the
    /// same status lists the sign-in and refresh tests read, so the public rule and the client's behaviour cannot
    /// be pinned to different lists.
    /// </summary>
    [TestClass]
    public class RefusalStatusTests
    {
        [DataTestMethod]
        [DynamicData(nameof(ClientTestFactory.RefusalStatuses), typeof(ClientTestFactory))]
        public void IsRefusalStatus_RefusalStatus_ReturnsTrue(HttpStatusCode statusCode)
        {
            Assert.IsTrue(AuthorizedHttpClient.IsRefusalStatus(statusCode));
        }

        [DataTestMethod]
        [DynamicData(nameof(ClientTestFactory.ServerFailureStatuses), typeof(ClientTestFactory))]
        public void IsRefusalStatus_ServerFailureStatus_ReturnsFalse(HttpStatusCode statusCode)
        {
            Assert.IsFalse(AuthorizedHttpClient.IsRefusalStatus(statusCode));
        }

        [TestMethod]
        public void IsRefusalStatus_SuccessStatus_ReturnsFalse()
        {
            Assert.IsFalse(AuthorizedHttpClient.IsRefusalStatus(HttpStatusCode.OK));
        }
    }
}
