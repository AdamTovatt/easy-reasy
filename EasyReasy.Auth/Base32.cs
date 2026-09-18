namespace EasyReasy.Auth
{
    /// <summary>
    /// RFC 4648 base32 codec, encoding without padding. A generic codec with no MFA-specific
    /// behaviour; TOTP enrollment uses it for the <c>secret=</c> parameter of the <c>otpauth://</c>
    /// URI and for the secret a user types into an authenticator app by hand, and
    /// <see cref="Decode"/> for the reverse direction — taking in a secret a user pastes back,
    /// whether from an existing enrollment being imported or a recovery flow.
    /// </summary>
    /// <remarks>
    /// The plain name is deliberate: it is also what a consumer's own copy of this codec is called,
    /// and a public name picked to dodge that collision would be worse for every caller forever than
    /// the collision is for the one caller that meets it. The README's migration notes cover resolving
    /// it.
    /// </remarks>
    public static class Base32
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        // The inputs this codec exists for are secrets, which encode to a few dozen characters. The
        // heap path exists only so a caller passing something large cannot overflow the stack.
        private const int MaxStackAllocatedChars = 256;

        /// <summary>
        /// Encodes bytes to an unpadded base32 string.
        /// </summary>
        /// <param name="data">The bytes to encode.</param>
        /// <returns>The base32 representation of <paramref name="data"/>, without <c>=</c> padding;
        /// an empty string for empty input.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="data"/> is so long that its
        /// base32 form could not be held in a string.</exception>
        public static string Encode(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                return string.Empty;
            }

            // Five bits per character, so exactly ceil(8n/5) characters — the last one zero-padded.
            // Computed in long deliberately: the multiplication overflows int at 256 MB of input, and
            // a negative length would pass the stack-allocation guard below and take down the process.
            long characterCount = ((long)data.Length * 8 + 4) / 5;
            if (characterCount > Array.MaxLength)
            {
                throw new ArgumentOutOfRangeException(nameof(data), $"Cannot base32-encode {data.Length} bytes: the result would be longer than the largest string.");
            }

            int length = (int)characterCount;
            Span<char> encoded = length <= MaxStackAllocatedChars ? stackalloc char[length] : new char[length];

            int written = 0;
            int buffer = 0;
            int bitsLeft = 0;
            foreach (byte value in data)
            {
                buffer = (buffer << 8) | value;
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    int index = (buffer >> (bitsLeft - 5)) & 0x1F;
                    bitsLeft -= 5;
                    encoded[written++] = Alphabet[index];
                }
            }

            if (bitsLeft > 0)
            {
                int index = (buffer << (5 - bitsLeft)) & 0x1F;
                encoded[written++] = Alphabet[index];
            }

            return new string(encoded);
        }

        /// <summary>
        /// Decodes a base32 string. Lenient about how the characters are presented, because a human
        /// types or pastes them: whitespace is ignored wherever it appears (so the space-grouped
        /// layout authenticator apps display decodes as shown), trailing <c>=</c> padding is tolerated
        /// (so input from a padding encoder decodes unchanged), and the alphabet is matched
        /// case-insensitively. Input carrying no base32 characters at all — empty, only whitespace,
        /// only padding — decodes to an empty array.
        /// </summary>
        /// <remarks>
        /// Strict about the characters themselves, which is the opposite concern: a character outside
        /// the alphabet, a character count no encoding could have produced, and a final character
        /// carrying bits past the last whole byte are all rejected rather than skipped or truncated.
        /// Together those catch most single-character slips in a hand-typed secret, though not every
        /// one — this is a guard against decoding into a silently different secret, not a checksum.
        /// </remarks>
        /// <param name="input">The base32 string to decode.</param>
        /// <returns>The decoded bytes; an empty array when <paramref name="input"/> carries no base32 characters.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="input"/> is <c>null</c>.</exception>
        /// <exception cref="FormatException"><paramref name="input"/> contains a character outside the
        /// RFC 4648 base32 alphabet (an <c>=</c> anywhere but the end included), carries a number of
        /// base32 characters that cannot be the encoding of any byte sequence, or ends in a character
        /// whose trailing bits are not the zero-padding an encoder writes.</exception>
        public static byte[] Decode(string input)
        {
            ArgumentNullException.ThrowIfNull(input);

            ReadOnlySpan<char> normalized = input.AsSpan().Trim().TrimEnd('=');
            if (normalized.IsEmpty)
            {
                return Array.Empty<byte>();
            }

            // Translate first, count second: an input that is both misspelled and mis-sized should be
            // reported as the character a reader can see is wrong, not as a length. Translating here
            // also leaves one expression of "is this a base32 character", which the sizing below and
            // the packing further down both depend on agreeing with.
            Span<byte> values = normalized.Length <= MaxStackAllocatedChars ? stackalloc byte[normalized.Length] : new byte[normalized.Length];
            int characterCount = 0;
            foreach (char character in normalized)
            {
                if (char.IsWhiteSpace(character))
                {
                    continue;
                }

                int index = Alphabet.IndexOf(char.ToUpperInvariant(character));
                if (index < 0)
                {
                    throw new FormatException($"Invalid base32 character '{character}'.");
                }

                values[characterCount++] = (byte)index;
            }

            // Eight characters carry five bytes, and a final group of 2, 4, 5 or 7 carries a whole
            // number of bytes plus zero-padding. A remainder of 1, 3 or 6 is no group the encoder can
            // emit, so the input lost or gained a character — decoding it anyway would silently discard
            // the bits that do not fill a byte and hand back a plausible wrong secret.
            int remainder = characterCount % 8;
            if (remainder is 1 or 3 or 6)
            {
                throw new FormatException($"Invalid base32 input: {characterCount} characters cannot be the encoding of any byte sequence.");
            }

            byte[] output = new byte[(int)((long)characterCount * 5 / 8)];

            int written = 0;
            int buffer = 0;
            int bitsLeft = 0;
            foreach (byte value in values[..characterCount])
            {
                buffer = (buffer << 5) | value;
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    output[written++] = (byte)((buffer >> (bitsLeft - 8)) & 0xFF);
                    bitsLeft -= 8;
                }
            }

            // The encoder pads the final character with zero bits, so anything else in them is a
            // character that has been mistyped into a neighbour — the case a count of the right size
            // cannot catch on its own.
            if (bitsLeft > 0 && (buffer & ((1 << bitsLeft) - 1)) != 0)
            {
                throw new FormatException("Invalid base32 input: the last character carries bits past the final whole byte, which no encoding produces.");
            }

            return output;
        }
    }
}
