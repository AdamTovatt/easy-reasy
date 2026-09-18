using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// How strongly a WebAuthn ceremony requires the authenticator to verify the user — a PIN, a
    /// fingerprint or a face, as opposed to the mere touch that proves someone is present.
    /// </summary>
    /// <remarks>
    /// The same value is passed to the options generator and to the verifier: the options tell the browser
    /// what to ask the authenticator for, and the verifier enforces what the options required. Only
    /// <see cref="Required"/> is enforced — under <see cref="Preferred"/> an authenticator that cannot
    /// verify the user is expected to complete the ceremony with user presence alone, so failing on the
    /// cleared flag would reject exactly the authenticators the setting means to accommodate.
    /// <para>
    /// The member names on the wire are fixed by WebAuthn, so they are pinned here rather than derived from
    /// a naming policy a consumer can change.
    /// </para>
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter<WebAuthnUserVerificationRequirement>))]
    public enum WebAuthnUserVerificationRequirement
    {
        /// <summary>
        /// The authenticator must verify the user, and a ceremony whose authenticator data does not carry
        /// the user-verified flag fails. This is the setting that makes the factor biometric rather than
        /// possession-only.
        /// </summary>
        [JsonStringEnumMemberName("required")]
        Required,

        /// <summary>
        /// The authenticator verifies the user when it can. Verification is not enforced.
        /// </summary>
        [JsonStringEnumMemberName("preferred")]
        Preferred,

        /// <summary>
        /// The authenticator is asked not to verify the user. Verification is not enforced.
        /// </summary>
        [JsonStringEnumMemberName("discouraged")]
        Discouraged,
    }
}
