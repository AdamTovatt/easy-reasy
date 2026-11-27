namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// <see cref="SecureBrowserAddress"/> lets the browser be sent only to https, or to plain http on a host the browser
    /// treats as a secure context.
    /// </summary>
    [TestClass]
    public class SecureBrowserAddressTests
    {
        [TestMethod]
        [DataRow("https://app.example.test/callback")]
        [DataRow("http://127.0.0.1:8080/callback")]
        [DataRow("http://[::1]:8080/callback")]
        [DataRow("http://localhost:5000/callback")]
        [DataRow("http://app.localhost/callback")]
        public void TryParse_SecureContextAddress_IsAllowed(string address)
        {
            Assert.IsTrue(SecureBrowserAddress.TryParse(address, out Uri? uri));
            Assert.AreEqual(new Uri(address), uri);
            Assert.IsTrue(SecureBrowserAddress.IsAllowed(new Uri(address)));
        }

        [TestMethod]
        [DataRow("http://app.example.test/callback")]
        [DataRow("http://localhost.example.test/callback")]
        [DataRow("javascript:alert(document.domain)")]
        [DataRow("data:text/html,hi")]
        [DataRow("ftp://app.example.test/file")]
        [DataRow("/callback")]
        [DataRow("")]
        [DataRow(null)]
        public void TryParse_AnyOtherAddress_IsRefused(string? address)
        {
            Assert.IsFalse(SecureBrowserAddress.TryParse(address, out Uri? uri));
            Assert.IsNull(uri);
        }

        [TestMethod]
        public void IsAllowed_RelativeOrNullUri_IsRefused()
        {
            Assert.IsFalse(SecureBrowserAddress.IsAllowed(new Uri("/callback", UriKind.Relative)));
            Assert.IsFalse(SecureBrowserAddress.IsAllowed(null));
        }
    }
}
