using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// What the relying party asks of the authenticator a user is about to register
    /// (WebAuthn Level 3 §5.4.4).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The resident-key fields come from which of the two registration paths built the options, and from
    /// nothing else: the constructor is private, the internal <c>ForSecondFactor</c> and
    /// <c>ForDiscoverable</c> factories are the only ways in, and <see cref="WebAuthnOptionsGenerator"/>
    /// is the only code that calls them.
    /// </para>
    /// <para>
    /// <b>Second factor</b> (<c>CreateRegistrationOptions</c>) asks for a <c>discouraged</c> resident key.
    /// The credential is only ever asked for by id, for a user a first factor has already named, so it
    /// never has to be found without knowing who the user is, and asking for a discoverable one anyway
    /// would consume a slot on a security key (they hold very few) for a capability nothing uses.
    /// </para>
    /// <para>
    /// <b>Passkey sign-in</b> (<c>CreateDiscoverableRegistrationOptions</c>) asks for a <c>required</c>
    /// resident key and always requires user verification. The credential is what names the user, found
    /// by the browser with nothing typed, so it must be discoverable; and with no first factor in front of
    /// it, the verified user is the whole of the login. A client that cannot create a discoverable
    /// credential fails the ceremony in the browser rather than quietly creating another kind (§5.4.4), so
    /// a registration that verifies is one the authenticator can find again.
    /// </para>
    /// </remarks>
    public sealed class AuthenticatorSelectionCriteria
    {
        /// <summary>The resident-key setting of the second-factor path.</summary>
        private const string DiscouragedResidentKey = "discouraged";

        /// <summary>The resident-key setting of the passkey sign-in path.</summary>
        private const string RequiredResidentKey = "required";

        /// <summary>
        /// Which kind of authenticator to steer the user towards, or null to leave the choice open.
        /// </summary>
        [JsonPropertyName("authenticatorAttachment")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WebAuthnAuthenticatorAttachment? AuthenticatorAttachment { get; }

        /// <summary>
        /// <c>required</c> for passkey sign-in, <c>discouraged</c> for a second factor; see the remarks on
        /// this type.
        /// </summary>
        [JsonPropertyName("residentKey")]
        public string ResidentKey => RequireResidentKey ? RequiredResidentKey : DiscouragedResidentKey;

        /// <summary>
        /// The counterpart of <see cref="ResidentKey"/> for older clients: true exactly when it is
        /// <c>required</c>. <see cref="ResidentKey"/> is derived from this, so the two cannot disagree.
        /// </summary>
        [JsonPropertyName("requireResidentKey")]
        public bool RequireResidentKey { get; }

        /// <summary>How strongly the authenticator is asked to verify the user.</summary>
        [JsonPropertyName("userVerification")]
        public WebAuthnUserVerificationRequirement UserVerification { get; }

        /// <summary>
        /// Initializes the selection criteria. Private: the two factories below are the only ways in, so a
        /// discoverable credential cannot be asked for with anything weaker than required user
        /// verification.
        /// </summary>
        private AuthenticatorSelectionCriteria(
            WebAuthnUserVerificationRequirement userVerification,
            WebAuthnAuthenticatorAttachment? authenticatorAttachment,
            bool requireResidentKey)
        {
            UserVerification = userVerification;
            AuthenticatorAttachment = authenticatorAttachment;
            RequireResidentKey = requireResidentKey;
        }

        /// <summary>
        /// The criteria for registering a second factor: a <c>discouraged</c> resident key and the user
        /// verification the application chose. Internal: everything a caller chooses here reaches it
        /// through <see cref="WebAuthnOptionsGenerator"/>, which is where the values are validated.
        /// </summary>
        /// <param name="userVerification">How strongly the authenticator is asked to verify the user.</param>
        /// <param name="authenticatorAttachment">Which kind of authenticator to steer towards, or null to leave the choice open.</param>
        /// <returns>The criteria to write into the creation options.</returns>
        internal static AuthenticatorSelectionCriteria ForSecondFactor(
            WebAuthnUserVerificationRequirement userVerification,
            WebAuthnAuthenticatorAttachment? authenticatorAttachment)
        {
            return new AuthenticatorSelectionCriteria(userVerification, authenticatorAttachment, requireResidentKey: false);
        }

        /// <summary>
        /// The criteria for registering a passkey: a <c>required</c> resident key and required user
        /// verification, neither of them a choice.
        /// </summary>
        /// <param name="authenticatorAttachment">Which kind of authenticator to steer towards, or null to leave the choice open.</param>
        /// <returns>The criteria to write into the creation options.</returns>
        internal static AuthenticatorSelectionCriteria ForDiscoverable(WebAuthnAuthenticatorAttachment? authenticatorAttachment)
        {
            return new AuthenticatorSelectionCriteria(WebAuthnUserVerificationRequirement.Required, authenticatorAttachment, requireResidentKey: true);
        }
    }
}
