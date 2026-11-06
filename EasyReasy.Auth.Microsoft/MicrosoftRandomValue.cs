using System.Buffers.Text;
using System.Security.Cryptography;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The one way this package makes an unguessable value: cryptographically random bytes, as base64url so the
    /// value is safe in a URL, a cookie name and a cookie value alike.
    /// </summary>
    internal static class MicrosoftRandomValue
    {
        /// <summary>
        /// Creates a random value.
        /// </summary>
        /// <param name="byteCount">How many random bytes it holds.</param>
        /// <returns>The bytes as unpadded base64url.</returns>
        public static string Create(int byteCount)
        {
            return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(byteCount));
        }
    }
}
