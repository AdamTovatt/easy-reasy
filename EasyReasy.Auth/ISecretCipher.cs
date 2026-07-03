namespace EasyReasy.Auth
{
    /// <summary>
    /// Symmetric authenticated encryption for small secrets that must be recoverable in plaintext
    /// to be used (unlike a one-way password hash) — for example a per-user TOTP shared secret held
    /// encrypted at rest. The intended pattern is to keep the key outside the datastore (e.g. in an
    /// environment variable) so a database or backup dump alone cannot recover the secret.
    /// </summary>
    /// <remarks>
    /// Output is a self-describing envelope with a leading version byte, so the format can evolve
    /// and <see cref="Decrypt"/> needs no out-of-band metadata. <see cref="EnvelopeVersion"/> exposes
    /// that byte so callers can persist it alongside the ciphertext and see which format a stored
    /// value uses without opening the envelope — useful when migrating to a later envelope layout.
    /// </remarks>
    public interface ISecretCipher
    {
        /// <summary>
        /// The envelope format version — the leading byte of every ciphertext produced by
        /// <see cref="Encrypt"/>. Increases when the envelope layout changes.
        /// </summary>
        byte EnvelopeVersion { get; }

        /// <summary>
        /// Encrypts <paramref name="plaintext"/> into a self-describing envelope.
        /// </summary>
        /// <param name="plaintext">The secret bytes to encrypt.</param>
        /// <returns>A new envelope of <c>[version][nonce][tag][ciphertext]</c> that <see cref="Decrypt"/> can recover.</returns>
        byte[] Encrypt(ReadOnlySpan<byte> plaintext);

        /// <summary>
        /// Decrypts an envelope produced by <see cref="Encrypt"/>.
        /// </summary>
        /// <param name="envelope">An envelope previously produced by <see cref="Encrypt"/>.</param>
        /// <returns>The recovered plaintext bytes.</returns>
        /// <exception cref="System.Security.Cryptography.CryptographicException">The envelope is
        /// malformed, its version is unsupported, or the authentication tag does not verify
        /// (tamper / wrong key).</exception>
        byte[] Decrypt(ReadOnlySpan<byte> envelope);
    }
}
