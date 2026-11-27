using System.Buffers.Text;
using System.Security.Cryptography;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// Random values for the flow id, the binding cookie and the completion code.
    /// </summary>
    internal static class SamlRandomValue
    {
        /// <summary>
        /// Returns <paramref name="byteCount"/> random bytes, base64url-encoded.
        /// </summary>
        public static string Create(int byteCount)
        {
            return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(byteCount));
        }
    }
}
