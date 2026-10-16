namespace EasyReasy.Auth
{
    /// <summary>
    /// An <see cref="ISecretCipher"/> whose key can be rotated without downtime: it encrypts under one
    /// current key, decrypts under any key it holds, and can tell which stored envelopes are stale and
    /// move them to the current key, so old values can be migrated in the background and the old key
    /// retired once none remain.
    /// </summary>
    /// <remarks>
    /// An envelope is stale when it differs from what <see cref="ISecretCipher.Encrypt"/> writes now,
    /// in either its <see cref="ISecretCipher.EnvelopeVersion"/> or its key id. Two ways to find stale
    /// values: persist <see cref="ISecretCipher.EnvelopeVersion"/> and <see cref="CurrentKeyId"/> beside
    /// each ciphertext and query for rows that differ from the cipher's, or call <see cref="IsStale"/>
    /// on each envelope.
    /// </remarks>
    public interface IRotatableSecretCipher : ISecretCipher
    {
        /// <summary>
        /// The id of the key <see cref="ISecretCipher.Encrypt"/> seals with, which every envelope it
        /// writes records. Null when the cipher writes envelopes that carry no key id (version 1).
        /// </summary>
        byte? CurrentKeyId { get; }

        /// <summary>
        /// Reports whether <paramref name="envelope"/> was written in a different envelope version or
        /// under a different key from what <see cref="ISecretCipher.Encrypt"/> writes now, and so
        /// should be passed to <see cref="ReEncrypt"/>. Reads the header only: it does not
        /// authenticate the envelope, and an envelope naming a key this cipher does not hold is stale.
        /// </summary>
        /// <param name="envelope">A stored envelope.</param>
        /// <returns>True when the envelope should be re-encrypted under the current key.</returns>
        /// <exception cref="System.Security.Cryptography.CryptographicException">The envelope is
        /// malformed or its version is unsupported.</exception>
        bool IsStale(ReadOnlySpan<byte> envelope);

        /// <summary>
        /// Opens <paramref name="envelope"/> and seals its plaintext again under the current key, so the
        /// caller never handles the plaintext. An envelope that is not stale is authenticated and comes
        /// back unchanged.
        /// </summary>
        /// <param name="envelope">A stored envelope sealed by any key this cipher holds.</param>
        /// <returns>An envelope under the current key, holding the same plaintext.</returns>
        /// <exception cref="System.Security.Cryptography.CryptographicException">The envelope cannot be
        /// decrypted, for any reason <see cref="ISecretCipher.Decrypt"/> gives.</exception>
        byte[] ReEncrypt(ReadOnlySpan<byte> envelope);
    }
}
