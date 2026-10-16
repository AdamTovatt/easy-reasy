using System.Security.Cryptography;

namespace EasyReasy.Auth
{
    /// <summary>
    /// AES-256-GCM implementation of <see cref="ISecretCipher"/> and <see cref="IRotatableSecretCipher"/>.
    /// GCM provides confidentiality and integrity in one pass, so a tampered or wrong-key ciphertext
    /// fails closed at <see cref="Decrypt"/> rather than returning garbage. A fresh random nonce is
    /// generated per encryption (never reused with the same key, which would be catastrophic for GCM).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two envelope layouts exist. Version 1 is <c>[version=1][nonce:12][tag:16][ciphertext:n]</c> and
    /// names no key. Version 2 is <c>[version=2][keyId:1][nonce:12][tag:16][ciphertext:n]</c>. Everything
    /// before the nonce is bound as associated data, so it is authenticated alongside the ciphertext: a
    /// swapped version byte or key id fails the tag check instead of opening under another key.
    /// </para>
    /// <para>
    /// The single-key constructor writes version 1 envelopes, exactly as before key rotation existed.
    /// It also opens version 2 envelopes with its one key whatever their key id, so rolling back from
    /// a keyring to a single key does not strand values written in between; the id is authenticated,
    /// so an envelope sealed by another key still fails the tag check. The keyring constructor writes
    /// version 2 envelopes under its current key and opens envelopes sealed by any key it holds.
    /// </para>
    /// </remarks>
    public sealed class AesGcmSecretCipher : IRotatableSecretCipher
    {
        private const byte VersionOne = 1;
        private const byte VersionTwo = 2;

        private const int KeySize = 32; // AES-256
        private const int NonceSize = 12; // 96-bit nonce — the GCM standard / AesGcm default
        private const int TagSize = 16; // 128-bit authentication tag

        // The prefix is everything before the nonce, and it is the associated data: the version byte,
        // then in version 2 the key id.
        private const int VersionOnePrefixSize = 1;
        private const int VersionTwoPrefixSize = 2;
        private const int VersionOneHeaderSize = VersionOnePrefixSize + NonceSize + TagSize;
        private const int VersionTwoHeaderSize = VersionTwoPrefixSize + NonceSize + TagSize;

        private const string TooShortMessage = "Ciphertext envelope is too short to be valid.";

        // Null for the single-key constructor, which writes version 1 envelopes; set, version 2.
        private readonly byte? _currentKeyId;
        private readonly byte[] _currentKey;
        private readonly byte[]? _versionOneKey;

        // Null for the single-key constructor, which opens a version 2 envelope of any id with its one key.
        private readonly IReadOnlyDictionary<byte, byte[]>? _keysById;

        /// <summary>
        /// Initializes the cipher with a single 32-byte (AES-256) key. It writes version 1 envelopes,
        /// which carry no key id, and its <see cref="CurrentKeyId"/> is null.
        /// </summary>
        /// <param name="key">The 256-bit symmetric key.</param>
        /// <exception cref="ArgumentException">The key is not exactly 32 bytes.</exception>
        public AesGcmSecretCipher(byte[] key)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (key.Length != KeySize)
            {
                throw new ArgumentException($"AES-256 key must be exactly {KeySize} bytes; got {key.Length}.", nameof(key));
            }

            _currentKeyId = null;
            _currentKey = (byte[])key.Clone();
            _versionOneKey = _currentKey;
            _keysById = null;
        }

        /// <summary>
        /// Initializes the cipher with a keyring for key rotation. It writes version 2 envelopes under
        /// the key named by <paramref name="currentKeyId"/>, and opens envelopes sealed by any key in
        /// <paramref name="keys"/>.
        /// </summary>
        /// <param name="keys">Every key the cipher holds, each 32 bytes (AES-256) with its own id.</param>
        /// <param name="currentKeyId">The id of the held key that new envelopes are sealed with.</param>
        /// <param name="versionOneKeyId">The id of the held key that opens version 1 envelopes, which
        /// record no key id: the key that was in use before rotation. Null when no version 1 values
        /// remain, after which a version 1 envelope fails to decrypt.</param>
        /// <exception cref="ArgumentNullException"><paramref name="keys"/> is null.</exception>
        /// <exception cref="ArgumentException">The keyring is invalid: it is empty, holds a null entry,
        /// a key that is not 32 bytes or two keys with one id, or a named id is not held. Every problem
        /// is reported in one exception.</exception>
        public AesGcmSecretCipher(IEnumerable<SecretCipherKey> keys, byte currentKeyId, byte? versionOneKeyId)
        {
            ArgumentNullException.ThrowIfNull(keys);

            Dictionary<byte, byte[]> keysById = ValidateKeyring(keys, currentKeyId, versionOneKeyId);

            _currentKeyId = currentKeyId;
            _currentKey = keysById[currentKeyId];
            _versionOneKey = versionOneKeyId is byte openingKeyId ? keysById[openingKeyId] : null;
            _keysById = keysById;
        }

        /// <inheritdoc />
        public byte EnvelopeVersion => _currentKeyId.HasValue ? VersionTwo : VersionOne;

        /// <inheritdoc />
        public byte? CurrentKeyId => _currentKeyId;

        /// <inheritdoc />
        public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
        {
            int prefixSize = _currentKeyId.HasValue ? VersionTwoPrefixSize : VersionOnePrefixSize;
            byte[] envelope = new byte[GetHeaderSize(prefixSize) + plaintext.Length];
            envelope[0] = EnvelopeVersion;
            if (_currentKeyId is byte currentKeyId)
            {
                envelope[1] = currentKeyId;
            }

            Span<byte> nonce = envelope.AsSpan(prefixSize, NonceSize);
            Span<byte> tag = envelope.AsSpan(prefixSize + NonceSize, TagSize);
            Span<byte> ciphertext = envelope.AsSpan(GetHeaderSize(prefixSize));

            RandomNumberGenerator.Fill(nonce);

            using AesGcm aes = new AesGcm(_currentKey, TagSize);
            // Bind the prefix (version byte, and key id in version 2) as associated data so it is covered
            // by the authentication tag: a swapped version byte or key id fails to verify.
            aes.Encrypt(nonce, plaintext, ciphertext, tag, envelope.AsSpan(0, prefixSize));

            return envelope;
        }

        /// <inheritdoc />
        /// <exception cref="CryptographicException">The envelope is malformed, its version is
        /// unsupported, it names a key this cipher does not hold (the message names the id), it is
        /// version 1 and no key is named to open version 1 envelopes, or the authentication tag does
        /// not verify (tamper / wrong key).</exception>
        public byte[] Decrypt(ReadOnlySpan<byte> envelope)
        {
            int prefixSize = ReadPrefixSize(envelope);
            byte[] key = prefixSize == VersionOnePrefixSize ? GetVersionOneKey() : GetKeyById(envelope[1]);

            ReadOnlySpan<byte> nonce = envelope.Slice(prefixSize, NonceSize);
            ReadOnlySpan<byte> tag = envelope.Slice(prefixSize + NonceSize, TagSize);
            ReadOnlySpan<byte> ciphertext = envelope.Slice(GetHeaderSize(prefixSize));

            byte[] plaintext = new byte[ciphertext.Length];

            using AesGcm aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, envelope.Slice(0, prefixSize));

            return plaintext;
        }

        /// <inheritdoc />
        public bool IsStale(ReadOnlySpan<byte> envelope)
        {
            int prefixSize = ReadPrefixSize(envelope);
            if (envelope[0] != EnvelopeVersion)
            {
                return true;
            }

            return prefixSize == VersionTwoPrefixSize && envelope[1] != _currentKeyId;
        }

        /// <inheritdoc />
        public byte[] ReEncrypt(ReadOnlySpan<byte> envelope)
        {
            // Work on one private copy, so the bytes returned unchanged are the bytes Decrypt
            // authenticated, even if the caller's buffer changes meanwhile.
            byte[] ownEnvelope = envelope.ToArray();

            // Decrypt first, so an envelope that is not stale is still authenticated before it is returned.
            byte[] plaintext = Decrypt(ownEnvelope);
            try
            {
                return IsStale(ownEnvelope) ? Encrypt(plaintext) : ownEnvelope;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        private static Dictionary<byte, byte[]> ValidateKeyring(IEnumerable<SecretCipherKey> keys, byte currentKeyId, byte? versionOneKeyId)
        {
            List<string> errors = new List<string>();
            Dictionary<byte, byte[]> keysById = new Dictionary<byte, byte[]>();
            HashSet<byte> reportedDuplicateIds = new HashSet<byte>();
            int index = 0;

            foreach (SecretCipherKey? key in keys)
            {
                if (key == null)
                {
                    errors.Add($"'{nameof(keys)}' holds null at index {index}.");
                }
                else
                {
                    if (key.Key.Length != KeySize)
                    {
                        errors.Add($"Key {key.Id} must be exactly {KeySize} bytes for AES-256; got {key.Key.Length}.");
                    }

                    if (!keysById.TryAdd(key.Id, key.Key) && reportedDuplicateIds.Add(key.Id))
                    {
                        errors.Add($"Key id {key.Id} is held more than once; every key needs its own id.");
                    }
                }

                index++;
            }

            if (index == 0)
            {
                errors.Add($"'{nameof(keys)}' must hold at least one key.");
            }

            if (!keysById.ContainsKey(currentKeyId))
            {
                errors.Add($"'{nameof(currentKeyId)}' is {currentKeyId}, but no held key has that id.");
            }

            if (versionOneKeyId is byte heldVersionOneKeyId && !keysById.ContainsKey(heldVersionOneKeyId))
            {
                errors.Add($"'{nameof(versionOneKeyId)}' is {heldVersionOneKeyId}, but no held key has that id.");
            }

            if (errors.Count > 0)
            {
                throw new ArgumentException($"Invalid secret cipher keyring:\n{string.Join("\n", errors)}");
            }

            return keysById;
        }

        private static int GetHeaderSize(int prefixSize)
        {
            return prefixSize + NonceSize + TagSize;
        }

        private static int ReadPrefixSize(ReadOnlySpan<byte> envelope)
        {
            // Version 1 has the shortest header, so checking it before the version byte keeps every
            // message a version 1 cipher gave for malformed input.
            if (envelope.Length < VersionOneHeaderSize)
            {
                throw new CryptographicException(TooShortMessage);
            }

            switch (envelope[0])
            {
                case VersionOne:
                    return VersionOnePrefixSize;
                case VersionTwo:
                    if (envelope.Length < VersionTwoHeaderSize)
                    {
                        throw new CryptographicException(TooShortMessage);
                    }

                    return VersionTwoPrefixSize;
                default:
                    throw new CryptographicException($"Unsupported ciphertext envelope version {envelope[0]}.");
            }
        }

        private byte[] GetVersionOneKey()
        {
            if (_versionOneKey == null)
            {
                throw new CryptographicException("Ciphertext envelope is version 1, which records no key id, and this cipher names no key to open version 1 envelopes.");
            }

            return _versionOneKey;
        }

        private byte[] GetKeyById(byte keyId)
        {
            if (_keysById == null)
            {
                // Single-key cipher: try its one key whatever the id. The id is authenticated, so an
                // envelope sealed by another key fails the tag check rather than opening. That failure is
                // left unwrapped, naming no id: a wrong key and tampering look the same to GCM, and
                // AuthenticationTagMismatchException is what callers of the single-key cipher already catch.
                return _currentKey;
            }

            if (!_keysById.TryGetValue(keyId, out byte[]? key))
            {
                throw new CryptographicException($"Ciphertext envelope was sealed by key id {keyId}, which this cipher does not hold.");
            }

            return key;
        }
    }
}
