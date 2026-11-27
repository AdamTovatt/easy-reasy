using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The hash under which a request id, a completion code or a binding cookie value is stored, so the store and the
    /// sealed state never hold the value itself.
    /// </summary>
    internal static class SamlHash
    {
        /// <summary>
        /// Returns the SHA-256 of the value's UTF-8 bytes, base64url-encoded.
        /// </summary>
        public static string Sha256(string value)
        {
            return Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        }
    }
}
