using System.Buffers.Text;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The one place this library spells base64url — the URL-safe alphabet with no padding, which is what
    /// WebAuthn's JSON contract carries and what the refresh and password-reset tokens are made of.
    /// </summary>
    /// <remarks>
    /// base64url and base64 differ in two characters and in padding, which is enough for a value encoded as
    /// one and read as the other to be wrong roughly half the time and right the rest — a failure that does
    /// not reproduce. Keeping one implementation is what stops the two from being confused a fourth time.
    /// </remarks>
    internal static class Base64UrlEncoding
    {
        /// <summary>
        /// Encodes bytes as base64url.
        /// </summary>
        /// <param name="value">The bytes to encode.</param>
        /// <returns>The base64url encoding, unpadded.</returns>
        public static string Encode(ReadOnlySpan<byte> value)
        {
            return Base64Url.EncodeToString(value);
        }

        /// <summary>
        /// Whether the given text is exactly what <see cref="Encode"/> would produce for the bytes it
        /// stands for: the URL-safe alphabet, no padding, no whitespace.
        /// </summary>
        /// <remarks>
        /// Stricter than simply decoding, because a value that merely decodes is not one a browser accepts.
        /// The <c>Base64URLString</c> the WebAuthn JSON contract defines is unpadded and whitespace-free, so
        /// a padded or spaced credential id that this library forwarded verbatim would produce a ceremony
        /// that never matches the credential and never says why.
        /// </remarks>
        /// <param name="value">The text to check.</param>
        /// <returns><c>true</c> when the text is canonical base64url.</returns>
        public static bool IsCanonical(string value)
        {
            if (!Base64Url.IsValid(value))
            {
                return false;
            }

            return string.Equals(Encode(Base64Url.DecodeFromChars(value)), value, StringComparison.Ordinal);
        }
    }
}
