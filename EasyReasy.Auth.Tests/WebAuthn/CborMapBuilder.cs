namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Assembles a definite-length CBOR map out of pre-encoded key and value fragments. Deliberately does
    /// no checking of its own: a test needs to be able to write a map with a repeated key, a key that is
    /// not a text string, or a value of a kind the parser does not expect, and a writer that refused those
    /// would put the parser's tolerance and duplicate-key handling out of reach.
    /// </summary>
    internal sealed class CborMapBuilder
    {
        /// <summary>Major type 5 (map) with the entry count in the low five bits.</summary>
        private const byte MapInitialByte = 0xA0;

        /// <summary>Largest entry count that fits in the initial byte; more than any test needs.</summary>
        private const int MaximumEntryCount = 23;

        private readonly List<byte[]> _fragments = new List<byte[]>();
        private int _entryCount;

        /// <summary>
        /// Appends one entry, as already-encoded CBOR.
        /// </summary>
        /// <param name="encodedKey">The encoded key.</param>
        /// <param name="encodedValue">The encoded value.</param>
        public CborMapBuilder With(byte[] encodedKey, byte[] encodedValue)
        {
            if (_entryCount == MaximumEntryCount)
            {
                throw new InvalidOperationException($"{nameof(CborMapBuilder)} writes the entry count into the initial byte, so it holds at most {MaximumEntryCount} entries.");
            }

            _fragments.Add(encodedKey);
            _fragments.Add(encodedValue);
            _entryCount++;

            return this;
        }

        /// <summary>
        /// Builds the map.
        /// </summary>
        public byte[] Build()
        {
            List<byte> encoded = new List<byte> { (byte)(MapInitialByte | _entryCount) };
            foreach (byte[] fragment in _fragments)
            {
                encoded.AddRange(fragment);
            }

            return encoded.ToArray();
        }
    }
}
