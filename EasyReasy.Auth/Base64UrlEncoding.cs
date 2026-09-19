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
    /// not reproduce. Keeping one implementation is what stops the two from being confused again, wherever
    /// the encoding is next needed.
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
        /// Decodes base64url, returning null when the text is not base64url at all.
        /// </summary>
        /// <remarks>
        /// Deliberately laxer than <see cref="TryDecodeCanonical"/>, and the two are used on opposite sides
        /// of the contract. Text that arrives from a browser is decoded with this: a client that pads its
        /// base64url has still named the right bytes, and refusing it would fail a ceremony over a spelling.
        /// Text an <i>application</i> hands back — a credential id, a stored challenge — came out of this
        /// library in canonical form, so anything else there is a value the application has mangled, and the
        /// canonical decoder rejects it rather than letting it fail later as a mismatch.
        /// </remarks>
        /// <param name="value">The text to decode.</param>
        /// <returns>The decoded bytes, or null when the text is not valid base64url.</returns>
        public static byte[]? TryDecode(string value)
        {
            if (!Base64Url.IsValid(value))
            {
                return null;
            }

            return Base64Url.DecodeFromChars(value);
        }

        /// <summary>
        /// Decodes text that is exactly what <see cref="Encode"/> would produce for the bytes it stands for
        /// — the URL-safe alphabet, no padding, no whitespace — and returns null for anything else.
        /// </summary>
        /// <remarks>
        /// Stricter than <see cref="TryDecode"/>, because a value that merely decodes is not one a browser
        /// accepts. The <c>Base64URLString</c> the WebAuthn JSON contract defines is unpadded and
        /// whitespace-free, so a padded or spaced credential id that this library forwarded verbatim would
        /// produce a ceremony that never matches the credential and never says why.
        /// <para>
        /// Returns the bytes rather than a bare verdict so a caller that needs both does not validate,
        /// decode and re-encode twice over — and so the decoded value cannot be obtained through a second
        /// call whose success the compiler has to be told about with a null-forgiving operator.
        /// </para>
        /// </remarks>
        /// <param name="value">The text to decode.</param>
        /// <returns>The decoded bytes, or null when the text is not canonical base64url.</returns>
        public static byte[]? TryDecodeCanonical(string value)
        {
            byte[]? decoded = TryDecode(value);
            if (decoded == null)
            {
                return null;
            }

            return string.Equals(Encode(decoded), value, StringComparison.Ordinal) ? decoded : null;
        }

        /// <summary>
        /// Whether the given text is canonical base64url, as <see cref="TryDecodeCanonical"/> defines it.
        /// </summary>
        /// <param name="value">The text to check.</param>
        /// <returns><c>true</c> when the text is canonical base64url.</returns>
        public static bool IsCanonical(string value)
        {
            return TryDecodeCanonical(value) != null;
        }
    }
}
