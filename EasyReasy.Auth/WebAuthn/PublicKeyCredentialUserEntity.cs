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
        /// Longest user handle an authenticator is required to store (WebAuthn Level 3 §5.4.3), and the
        /// length beyond which a client may truncate the two names.
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
        /// The account identifier to show the user, at most <see cref="MaximumIdLength"/> bytes of UTF-8.
        /// </param>
        /// <param name="displayName">The display name to show alongside it, under the same length limit.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="id"/> is empty, or any of the three is longer than <see cref="MaximumIdLength"/>
        /// bytes, or a name is empty.
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
        /// Rejects a name that is empty or longer than an authenticator is required to store. The length
        /// bound is here rather than left to the authenticator because an oversized value fails inside
        /// CTAP, where the application cannot tell which of the three fields was at fault.
        /// </summary>
        private static void ValidateName(string value, string parameterName, string description)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"A {description} must be non-empty.", parameterName);
            }

            int lengthInBytes = Encoding.UTF8.GetByteCount(value);
            if (lengthInBytes > MaximumIdLength)
            {
                throw new ArgumentException($"A {description} must be at most {MaximumIdLength} bytes of UTF-8; got {lengthInBytes}.", parameterName);
            }
        }
    }
}
