using System;
using System.IO;
using System.Security.Cryptography;

namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// Helper class for generating hashes from streams.
    /// </summary>
    public static class StreamHashHelper
    {
        /// <summary>
        /// Generates a SHA256 hash from the contents of a stream.
        /// The stream will be consumed and its position will be at the end after this operation.
        /// </summary>
        /// <param name="stream">The stream to hash.</param>
        /// <returns>A byte array containing the SHA256 hash.</returns>
        /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
        public static byte[] GenerateSha256Hash(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return SHA256.HashData(stream); // .NET 6+: avoids allocating a SHA256 instance
        }
    }
}
