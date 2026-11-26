using System.Buffers.Text;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class JsonSealerTests
    {
        [TestMethod]
        public void SealThenOpen_SameKey_RoundTripsTheValue()
        {
            JsonSealer sealer = new JsonSealer(SecretCipherTestKeys.NewKeyBytes());

            SealedSample? opened = sealer.Open<SealedSample>(sealer.Seal(new SealedSample { Name = "flow", Count = 3 }));

            Assert.IsNotNull(opened);
            Assert.AreEqual("flow", opened.Name);
            Assert.AreEqual(3, opened.Count);
        }

        [TestMethod]
        public void Seal_SameValueTwice_GivesDifferentTextThatHidesTheValue()
        {
            JsonSealer sealer = new JsonSealer(SecretCipherTestKeys.NewKeyBytes());
            SealedSample value = new SealedSample { Name = "readable-name", Count = 1 };

            string first = sealer.Seal(value);
            string second = sealer.Seal(value);

            Assert.AreNotEqual(first, second);
            Assert.IsTrue(Base64Url.IsValid(first));
            Assert.IsFalse(first.Contains("readable-name", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Open_SealedUnderAnotherKey_ReturnsNull()
        {
            string sealedValue = new JsonSealer(SecretCipherTestKeys.NewKeyBytes()).Seal(new SealedSample { Name = "flow", Count = 1 });

            Assert.IsNull(new JsonSealer(SecretCipherTestKeys.NewKeyBytes()).Open<SealedSample>(sealedValue));
        }

        [TestMethod]
        public void Open_OneCharacterChanged_ReturnsNull()
        {
            JsonSealer sealer = new JsonSealer(SecretCipherTestKeys.NewKeyBytes());
            string sealedValue = sealer.Seal(new SealedSample { Name = "flow", Count = 1 });
            int middle = sealedValue.Length / 2;
            string tampered = sealedValue[..middle] + (sealedValue[middle] == 'A' ? 'B' : 'A') + sealedValue[(middle + 1)..];

            Assert.IsNull(sealer.Open<SealedSample>(tampered));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("not base64url!")]
        [DataRow("AAAA")]
        public void Open_NotASealedValue_ReturnsNull(string? sealedValue)
        {
            Assert.IsNull(new JsonSealer(SecretCipherTestKeys.NewKeyBytes()).Open<SealedSample>(sealedValue));
        }

        [TestMethod]
        public void Open_SealedAsAnotherShape_ReturnsNull()
        {
            JsonSealer sealer = new JsonSealer(SecretCipherTestKeys.NewKeyBytes());

            // Sealed by this key, but lacking the required members: a value from an older layout of the type.
            Assert.IsNull(sealer.Open<SealedSample>(sealer.Seal(new { Other = "value" })));
        }

        [TestMethod]
        public void Constructor_NullKey_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new JsonSealer(null!));
        }

        [TestMethod]
        [DataRow(16)]
        [DataRow(33)]
        public void Constructor_KeyNot32Bytes_Throws(int keySize)
        {
            Assert.ThrowsException<ArgumentException>(() => new JsonSealer(new byte[keySize]));
        }

        [TestMethod]
        public void Constructor_KeyChangedAfterwards_StillOpensWhatItSealed()
        {
            byte[] key = SecretCipherTestKeys.NewKeyBytes();
            JsonSealer sealer = new JsonSealer(key);
            string sealedValue = sealer.Seal(new SealedSample { Name = "flow", Count = 1 });

            Array.Clear(key);

            Assert.IsNotNull(sealer.Open<SealedSample>(sealedValue));
        }

        private sealed class SealedSample
        {
            public required string Name { get; init; }

            public required int Count { get; init; }
        }
    }
}
