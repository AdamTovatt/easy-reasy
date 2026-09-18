using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// What the relying party asks of the authenticator a user is about to register
    /// (WebAuthn Level 3 §5.4.4).
    /// </summary>
    /// <remarks>
    /// The resident-key fields are fixed rather than configurable. A discoverable credential is what makes
    /// passwordless login possible, and that is explicitly out of scope: this library registers a second
    /// factor for a user who has already authenticated, so the credential never has to be found without
    /// knowing who the user is. Asking for one anyway would consume a slot on a security key — they hold
    /// very few — for a capability nothing here uses.
    /// </remarks>
    public sealed class AuthenticatorSelectionCriteria
    {
        /// <summary>The resident-key setting this library always asks for.</summary>
        private const string DiscouragedResidentKey = "discouraged";

        /// <summary>
        /// Which kind of authenticator to steer the user towards, or null to leave the choice open.
        /// </summary>
        [JsonPropertyName("authenticatorAttachment")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WebAuthnAuthenticatorAttachment? AuthenticatorAttachment { get; }

        /// <summary>Always <c>discouraged</c>; see the remarks on this type.</summary>
        [JsonPropertyName("residentKey")]
        public string ResidentKey => DiscouragedResidentKey;

        /// <summary>Always false, the counterpart of <see cref="ResidentKey"/> for older clients.</summary>
        [JsonPropertyName("requireResidentKey")]
        public bool RequireResidentKey => false;

        /// <summary>How strongly the authenticator is asked to verify the user.</summary>
        [JsonPropertyName("userVerification")]
        public WebAuthnUserVerificationRequirement UserVerification { get; }

        /// <summary>
        /// Initializes the selection criteria. Internal: everything a caller chooses here reaches it
        /// through <see cref="WebAuthnOptionsGenerator"/>, which is where the values are validated.
        /// </summary>
        /// <param name="userVerification">How strongly the authenticator is asked to verify the user.</param>
        /// <param name="authenticatorAttachment">Which kind of authenticator to steer towards, or null to leave the choice open.</param>
        internal AuthenticatorSelectionCriteria(
            WebAuthnUserVerificationRequirement userVerification,
            WebAuthnAuthenticatorAttachment? authenticatorAttachment)
        {
            UserVerification = userVerification;
            AuthenticatorAttachment = authenticatorAttachment;
        }
    }
}
