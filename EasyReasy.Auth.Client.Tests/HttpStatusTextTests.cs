using System.Net;

namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// The output format of <see cref="HttpStatusText.Describe"/>, which consumers match against the library's
    /// own messages.
    /// </summary>
    [TestClass]
    public class HttpStatusTextTests
    {
        [DataTestMethod]
        [DataRow(HttpStatusCode.OK, "200 OK")]
        [DataRow(HttpStatusCode.Unauthorized, "401 Unauthorized")]
        [DataRow(HttpStatusCode.NotFound, "404 NotFound")]
        [DataRow(HttpStatusCode.TooManyRequests, "429 TooManyRequests")]
        [DataRow(HttpStatusCode.ServiceUnavailable, "503 ServiceUnavailable")]
        public void Describe_NamedStatus_WritesNumberThenName(HttpStatusCode statusCode, string expected)
        {
            Assert.AreEqual(expected, HttpStatusText.Describe(statusCode));
        }

        [DataTestMethod]
        [DataRow(499, "499")]
        [DataRow(599, "599")]
        public void Describe_StatusWithNoName_WritesTheNumberAlone(int statusCode, string expected)
        {
            Assert.AreEqual(expected, HttpStatusText.Describe((HttpStatusCode)statusCode));
        }
    }
}
