using System.Text;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The user a credential is being registered for, as the browser's creation options name them
    /// (WebAuthn Level 3 §5.4.3).
    /// </summary>
    public sealed class PublicKeyCredentialUserEntity
    {
        /// <summary>
        /// Longest user handle WebAuthn Level 3 §5.4.3 permits. Applies to the handle alone: the two names
        /// carry no such limit, because the specification answers an oversized one by truncating rather
        /// than by failing.
        /// </summary>
        public const int MaximumIdLength = 64;

        /// <summary>
        /// The user handle, base64url-encoded as the browser's JSON contract carries it. Constructed from
        /// bytes, so a caller never has to know which of base64 and base64url this field takes.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; }

        /// <summary>
        /// The account identifier shown to the user when they pick between credentials — typically the
        /// username or email address they signed in with.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; }

        /// <summary>The display name shown alongside <see cref="Name"/>.</summary>
        [JsonPropertyName("displayName")]
        public string DisplayName { get; }

        /// <summary>
        /// Initializes the user entity.
        /// </summary>
        /// <param name="id">
        /// An opaque, stable handle for the user, at most <see cref="MaximumIdLength"/> bytes. It is stored
        /// on the authenticator and can be read back from it, so it must not carry personal data — use a
        /// surrogate key, not an email address.
        /// </param>
        /// <param name="name">
        /// The account identifier to show the user. Not length-limited here: WebAuthn lets a client or
        /// authenticator truncate a name it cannot store in full, so a long one is valid input and
        /// rejecting it would refuse an enrollment the browser would have completed — a user whose email
        /// address runs past 64 bytes could not enroll at all.
        /// </param>
        /// <param name="displayName">The display name to show alongside it, on the same terms.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="id"/> is empty or longer than <see cref="MaximumIdLength"/> bytes, or a name is
        /// empty.
        /// </exception>
        public PublicKeyCredentialUserEntity(byte[] id, string name, string displayName)
        {
            ArgumentNullException.ThrowIfNull(id);
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(displayName);

            if (id.Length == 0 || id.Length > MaximumIdLength)
            {
                throw new ArgumentException($"A user handle must be between 1 and {MaximumIdLength} bytes; got {id.Length}.", nameof(id));
            }

            ValidateName(name, nameof(name), "user name");
            ValidateName(displayName, nameof(displayName), "user display name");

            Id = Base64UrlEncoding.Encode(id);
            Name = name;
            DisplayName = displayName;
        }

        /// <summary>
        /// Rejects a name that is empty, which is the only thing wrong with a name this library can know.
        /// </summary>
        /// <remarks>
        /// Deliberately not length-limited. A 64-byte bound looks like the handle's and is a different rule:
        /// §5.4.3 says a user handle MUST NOT exceed 64 bytes, while a name that does not fit is truncated
        /// by whoever cannot store it. Enforcing the handle's rule on the names would reject valid input —
        /// an email address of 65 bytes is an ordinary one — and turn a display detail into a failed
        /// enrollment.
        /// </remarks>
        private static void ValidateName(string value, string parameterName, string description)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"A {description} must be non-empty.", parameterName);
            }
        }
    }
}
