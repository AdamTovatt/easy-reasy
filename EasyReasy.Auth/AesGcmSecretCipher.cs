using System.Security.Cryptography;

namespace EasyReasy.Auth
{
    /// <summary>
    /// AES-256-GCM implementation of <see cref="ISecretCipher"/>. The envelope layout is
    /// <c>[version:1][nonce:12][tag:16][ciphertext:n]</c>. GCM provides confidentiality and
    /// integrity in one pass, so a tampered or wrong-key ciphertext fails closed at
    /// <see cref="Decrypt"/> rather than returning garbage. The leading version byte is bound as
    /// associated data, so it is authenticated alongside the ciphertext. A fresh random nonce is
    /// generated per encryption (never reused with the same key, which would be catastrophic for GCM).
    /// </summary>
    public sealed class AesGcmSecretCipher : ISecretCipher
    {
        /// <summary>Current envelope format version. Bump when the envelope layout changes.</summary>
        private const byte CurrentVersion = 1;

        private const int KeySize = 32; // AES-256
        private const int NonceSize = 12; // 96-bit nonce — the GCM standard / AesGcm default
        private const int TagSize = 16; // 128-bit authentication tag
        private const int HeaderSize = 1 + NonceSize + TagSize;

        private readonly byte[] _key;

        /// <summary>
        /// Initializes the cipher with a 32-byte (AES-256) key.
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

            _key = (byte[])key.Clone();
        }

        /// <inheritdoc />
        public byte EnvelopeVersion => CurrentVersion;

        /// <inheritdoc />
        public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
        {
            byte[] envelope = new byte[HeaderSize + plaintext.Length];
            envelope[0] = CurrentVersion;

            Span<byte> nonce = envelope.AsSpan(1, NonceSize);
            Span<byte> tag = envelope.AsSpan(1 + NonceSize, TagSize);
            Span<byte> ciphertext = envelope.AsSpan(HeaderSize);

            RandomNumberGenerator.Fill(nonce);

            using AesGcm aes = new AesGcm(_key, TagSize);
            // Bind the version byte as associated data so it is covered by the authentication tag —
            // once more than one version is accepted, a swapped version byte fails to verify.
            aes.Encrypt(nonce, plaintext, ciphertext, tag, envelope.AsSpan(0, 1));

            return envelope;
        }

        /// <inheritdoc />
        public byte[] Decrypt(ReadOnlySpan<byte> envelope)
        {
            if (envelope.Length < HeaderSize)
            {
                throw new CryptographicException("Ciphertext envelope is too short to be valid.");
            }

            if (envelope[0] != CurrentVersion)
            {
                throw new CryptographicException($"Unsupported ciphertext envelope version {envelope[0]}.");
            }

            ReadOnlySpan<byte> nonce = envelope.Slice(1, NonceSize);
            ReadOnlySpan<byte> tag = envelope.Slice(1 + NonceSize, TagSize);
            ReadOnlySpan<byte> ciphertext = envelope.Slice(HeaderSize);

            byte[] plaintext = new byte[ciphertext.Length];

            using AesGcm aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, envelope.Slice(0, 1));

            return plaintext;
        }
    }
}
