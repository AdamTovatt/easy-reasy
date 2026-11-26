using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    [TestClass]
    public class OpenIdConnectPublicKeyTests
    {
        [TestMethod]
        public void ToJson_KeySetKeyWithACertificate_KeepsTheCertificateChainAndNoPrivateMember()
        {
            using RSA rsa = RSA.Create(2048);
            using X509Certificate2 certificate = new CertificateRequest("CN=issuer.example", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            JsonWebKey published = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(true)) { KeyId = "cert-key" });
            string chainElement = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));
            published.X5c.Add(chainElement);

            using JsonDocument key = JsonDocument.Parse(OpenIdConnectPublicKey.ToJson(published));

            Assert.AreEqual("cert-key", key.RootElement.GetProperty("kid").GetString());
            Assert.AreEqual(1, key.RootElement.GetProperty("x5c").GetArrayLength());
            Assert.AreEqual(chainElement, key.RootElement.GetProperty("x5c")[0].GetString());
            Assert.IsFalse(key.RootElement.TryGetProperty("d", out _));
        }
    }
}
