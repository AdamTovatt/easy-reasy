namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Covers the enrollment half of <see cref="Rfc6238TotpGenerator"/>: secret generation and the
    /// <c>otpauth://</c> provisioning URI. Code generation and validation live in
    /// <see cref="Rfc6238TotpGeneratorTests"/>.
    /// </summary>
    [TestClass]
    public class Rfc6238TotpProvisioningTests
    {
        private static readonly byte[] Seed = Rfc6238TestVectors.Seed;
        private const string SeedBase32 = Rfc6238TestVectors.SeedBase32;

        private readonly Rfc6238TotpGenerator _generator = new Rfc6238TotpGenerator();

        [TestMethod]
        public void BuildProvisioningUri_DefaultGenerator_EmitsKeyUriWithEscapedLabelAndDefaults()
        {
            // Issuer and account name are deliberately different strings: a URI that swapped them
            // would not match.
            string uri = _generator.BuildProvisioningUri("Acme Corp", "user@example.com", Seed);

            Assert.AreEqual(
                "otpauth://totp/Acme%20Corp%3Auser%40example.com" +
                $"?secret={SeedBase32}" +
                "&issuer=Acme%20Corp" +
                "&algorithm=SHA1" +
                "&digits=6" +
                "&period=30",
                uri);
        }

        [TestMethod]
        public void BuildProvisioningUri_CustomDigitsAndStep_EmitsTheInstanceValues()
        {
            Rfc6238TotpGenerator generator = new Rfc6238TotpGenerator(digits: 8, stepSeconds: 60);

            string uri = generator.BuildProvisioningUri("Acme Corp", "user@example.com", Seed);

            Assert.AreEqual(
                "otpauth://totp/Acme%20Corp%3Auser%40example.com" +
                $"?secret={SeedBase32}" +
                "&issuer=Acme%20Corp" +
                "&algorithm=SHA1" +
                "&digits=8" +
                "&period=60",
                uri);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("\t")]
        public void BuildProvisioningUri_BlankIssuer_Throws(string issuer)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => _generator.BuildProvisioningUri(issuer, "user@example.com", Seed));

            // The account name is a valid one, so a check that blamed the wrong argument would be
            // caught here rather than passing on the exception type alone.
            Assert.AreEqual("issuer", exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("\t")]
        public void BuildProvisioningUri_BlankAccountName_Throws(string accountName)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => _generator.BuildProvisioningUri("Acme Corp", accountName, Seed));

            Assert.AreEqual("accountName", exception.ParamName);
        }

        [TestMethod]
        public void BuildProvisioningUri_NullIssuer_Throws()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => _generator.BuildProvisioningUri(null!, "user@example.com", Seed));

            Assert.AreEqual("issuer", exception.ParamName);
        }

        [TestMethod]
        public void BuildProvisioningUri_NullAccountName_Throws()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => _generator.BuildProvisioningUri("Acme Corp", null!, Seed));

            Assert.AreEqual("accountName", exception.ParamName);
        }

        [TestMethod]
        public void BuildProvisioningUri_ColonInIssuer_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => _generator.BuildProvisioningUri("Acme:Corp", "user@example.com", Seed));

            Assert.AreEqual("issuer", exception.ParamName);
        }

        [TestMethod]
        public void BuildProvisioningUri_ColonInAccountName_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => _generator.BuildProvisioningUri("Acme Corp", "user:example.com", Seed));

            Assert.AreEqual("accountName", exception.ParamName);
        }

        [TestMethod]
        public void BuildProvisioningUri_EmptySecret_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => _generator.BuildProvisioningUri("Acme Corp", "user@example.com", Array.Empty<byte>()));

            Assert.AreEqual("secret", exception.ParamName);
        }

        [TestMethod]
        public void BuildProvisioningUri_SecretItCarries_ValidatesAgainstTheSameGenerator()
        {
            // The enrollment round trip an authenticator app performs: take the secret out of the
            // URI, compute a code from it, and present it to the generator that issued the URI.
            Rfc6238TotpGenerator generator = new Rfc6238TotpGenerator(digits: 8, stepSeconds: 60);
            byte[] secret = Rfc6238TotpGenerator.GenerateSecret();

            string uri = generator.BuildProvisioningUri("Acme Corp", "user@example.com", secret);

            // Parsing through Uri also pins that what is emitted is a well-formed URI.
            Uri parsed = new Uri(uri);
            string scannedBase32 = ReadQueryParameter(parsed, "secret");
            byte[] scannedSecret = Base32.Decode(scannedBase32);
            long step = generator.GetTimeStep(DateTimeOffset.UtcNow);
            string code = generator.Generate(scannedSecret, step);

            Assert.IsTrue(generator.TryValidate(secret, code, step, window: 0, out long matchedStep));
            Assert.AreEqual(step, matchedStep);
        }

        [TestMethod]
        public void GenerateSecret_ProducesTwentyBytes()
        {
            byte[] secret = Rfc6238TotpGenerator.GenerateSecret();

            Assert.AreEqual(20, secret.Length);
        }

        [TestMethod]
        public void GenerateSecret_ProducesADifferentSecretEachCall()
        {
            byte[] first = Rfc6238TotpGenerator.GenerateSecret();
            byte[] second = Rfc6238TotpGenerator.GenerateSecret();

            CollectionAssert.AreNotEqual(first, second);
        }

        private static string ReadQueryParameter(Uri uri, string name)
        {
            foreach (string pair in uri.Query.TrimStart('?').Split('&'))
            {
                string[] parts = pair.Split('=', 2);
                if (parts.Length == 2 && parts[0] == name)
                {
                    return parts[1];
                }
            }

            throw new AssertFailedException($"The provisioning URI carries no '{name}' parameter: {uri}");
        }
    }
}
