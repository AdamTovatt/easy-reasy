using System.Text;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class Rfc6238TotpGeneratorTests
    {
        // RFC 6238 Appendix B uses the ASCII seed "12345678901234567890" for the SHA-1 vectors.
        private static readonly byte[] Seed = Encoding.ASCII.GetBytes("12345678901234567890");

        private readonly Rfc6238TotpGenerator _generator = new Rfc6238TotpGenerator();

        // RFC 6238 Appendix B SHA-1 vectors, truncated to the 6 low digits of the published 8-digit code.
        [DataTestMethod]
        [DataRow(59L, "287082")]
        [DataRow(1111111109L, "081804")]
        [DataRow(1111111111L, "050471")]
        [DataRow(1234567890L, "005924")]
        [DataRow(2000000000L, "279037")]
        [DataRow(20000000000L, "353130")]
        public void Generate_Rfc6238Vectors_MatchSha1ReferenceCodes(long unixTime, string expected)
        {
            long step = _generator.GetTimeStep(DateTimeOffset.FromUnixTimeSeconds(unixTime));
            string code = _generator.Generate(Seed, step);
            Assert.AreEqual(expected, code);
        }

        // RFC 6238 Appendix B SHA-1 vectors at their native 8-digit length.
        [DataTestMethod]
        [DataRow(59L, "94287082")]
        [DataRow(1111111109L, "07081804")]
        [DataRow(1111111111L, "14050471")]
        [DataRow(1234567890L, "89005924")]
        [DataRow(2000000000L, "69279037")]
        [DataRow(20000000000L, "65353130")]
        public void Generate_Rfc6238Vectors_MatchSha1EightDigitReferenceCodes(long unixTime, string expected)
        {
            Rfc6238TotpGenerator eightDigit = new Rfc6238TotpGenerator(digits: 8);
            long step = eightDigit.GetTimeStep(DateTimeOffset.FromUnixTimeSeconds(unixTime));
            string code = eightDigit.Generate(Seed, step);
            Assert.AreEqual(expected, code);
        }

        [DataTestMethod]
        [DataRow(5)]
        [DataRow(9)]
        public void Constructor_DigitsOutOfRange_Throws(int digits)
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Rfc6238TotpGenerator(digits: digits));
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        public void Constructor_NonPositiveStepSeconds_Throws(int stepSeconds)
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Rfc6238TotpGenerator(stepSeconds: stepSeconds));
        }

        [TestMethod]
        public void GetTimeStep_HonorsCustomStepSeconds()
        {
            Rfc6238TotpGenerator sixtySecond = new Rfc6238TotpGenerator(stepSeconds: 60);
            Assert.AreEqual(0L, sixtySecond.GetTimeStep(DateTimeOffset.FromUnixTimeSeconds(59)));
            Assert.AreEqual(1L, sixtySecond.GetTimeStep(DateTimeOffset.FromUnixTimeSeconds(60)));
        }

        [TestMethod]
        public void TryValidate_RoundTripsWithCustomDigitsAndStep()
        {
            Rfc6238TotpGenerator custom = new Rfc6238TotpGenerator(digits: 8, stepSeconds: 60);
            long step = custom.GetTimeStep(DateTimeOffset.FromUnixTimeSeconds(1234567890));
            string code = custom.Generate(Seed, step);

            bool valid = custom.TryValidate(Seed, code, currentStep: step, window: 1, out long matchedStep);

            Assert.AreEqual(8, code.Length);
            Assert.IsTrue(valid);
            Assert.AreEqual(step, matchedStep);
        }

        [TestMethod]
        public void GetTimeStep_DividesUnixSecondsByThirty()
        {
            Assert.AreEqual(0L, _generator.GetTimeStep(DateTimeOffset.FromUnixTimeSeconds(29)));
            Assert.AreEqual(1L, _generator.GetTimeStep(DateTimeOffset.FromUnixTimeSeconds(30)));
            Assert.AreEqual(2L, _generator.GetTimeStep(DateTimeOffset.FromUnixTimeSeconds(75)));
        }

        [TestMethod]
        public void TryValidate_CorrectCodeForCurrentStep_ReturnsTrueWithMatchedStep()
        {
            long step = 1000;
            string code = _generator.Generate(Seed, step);

            bool valid = _generator.TryValidate(Seed, code, currentStep: step, window: 1, out long matchedStep);

            Assert.IsTrue(valid);
            Assert.AreEqual(step, matchedStep);
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(1)]
        public void TryValidate_CodeWithinClockSkewWindow_IsAccepted(int stepOffset)
        {
            long actualStep = 5000 + stepOffset;
            string code = _generator.Generate(Seed, actualStep);

            bool valid = _generator.TryValidate(Seed, code, currentStep: 5000, window: 1, out long matchedStep);

            Assert.IsTrue(valid);
            Assert.AreEqual(actualStep, matchedStep);
        }

        [TestMethod]
        public void TryValidate_CodeOutsideWindow_IsRejected()
        {
            string code = _generator.Generate(Seed, 5000 + 2);
            bool valid = _generator.TryValidate(Seed, code, currentStep: 5000, window: 1, out _);
            Assert.IsFalse(valid);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("12345")]   // too short
        [DataRow("1234567")] // too long
        [DataRow("12ab56")]  // non-digit
        public void TryValidate_MalformedCode_IsRejected(string code)
        {
            bool valid = _generator.TryValidate(Seed, code, currentStep: 5000, window: 1, out _);
            Assert.IsFalse(valid);
        }

        [TestMethod]
        public void TryValidate_CodeWithEmbeddedSpaces_IsAccepted()
        {
            long step = 5000;
            string code = _generator.Generate(Seed, step); // 6 digits, e.g. "123456"
            string spaced = code.Insert(3, " ");           // grouped display, e.g. "123 456"

            bool valid = _generator.TryValidate(Seed, spaced, currentStep: step, window: 1, out long matchedStep);

            Assert.IsTrue(valid);
            Assert.AreEqual(step, matchedStep);
        }

        [TestMethod]
        public void TryValidate_CodeWithSurroundingNonSpaceWhitespace_IsAccepted()
        {
            long step = 5000;
            string code = _generator.Generate(Seed, step);
            string wrapped = "\t" + code + "\n"; // tab/newline from a paste — stripped by Trim, not by the space replace

            bool valid = _generator.TryValidate(Seed, wrapped, currentStep: step, window: 1, out long matchedStep);

            Assert.IsTrue(valid);
            Assert.AreEqual(step, matchedStep);
        }

        [TestMethod]
        public void TryValidate_NegativeWindow_Throws()
        {
            string code = _generator.Generate(Seed, 5000);
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => _generator.TryValidate(Seed, code, currentStep: 5000, window: -1, out _));
        }

        [TestMethod]
        public void TryValidate_WrongSecret_IsRejected()
        {
            long step = 5000;
            string code = _generator.Generate(Seed, step);
            byte[] otherSecret = Encoding.ASCII.GetBytes("09876543210987654321");

            bool valid = _generator.TryValidate(otherSecret, code, currentStep: step, window: 1, out _);

            Assert.IsFalse(valid);
        }
    }
}
