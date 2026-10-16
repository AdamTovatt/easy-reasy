namespace EasyReasy.Auth
{
    /// <summary>
    /// One key in an <see cref="AesGcmSecretCipher"/> keyring: the key bytes and the id that every
    /// envelope sealed with them records. The id is chosen by the caller, so it stays the same across
    /// restarts and deployments; it is what lets a stored ciphertext name the key that opens it.
    /// </summary>
    /// <remarks>
    /// The key bytes are copied at construction, so changing the caller's array afterwards has no
    /// effect. The bytes are validated by the cipher that holds the key, which reports every problem
    /// with the keyring at once.
    /// </remarks>
    public sealed class SecretCipherKey
    {
        /// <summary>
        /// Initializes a keyring entry.
        /// </summary>
        /// <param name="id">The id recorded in every envelope this key seals, from 0 to 255.</param>
        /// <param name="key">The 256-bit symmetric key.</param>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
        public SecretCipherKey(byte id, byte[] key)
        {
            ArgumentNullException.ThrowIfNull(key);

            Id = id;
            Key = (byte[])key.Clone();
        }

        /// <summary>The id recorded in every envelope this key seals.</summary>
        public byte Id { get; }

        /// <summary>The cipher's own copy of the key bytes; never handed out.</summary>
        internal byte[] Key { get; }
    }
}
