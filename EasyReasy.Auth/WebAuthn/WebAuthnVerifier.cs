using System.Security.Cryptography;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Verifies the ceremonies a browser runs against one relying party, and reports what to store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pure and stateless, like <see cref="Rfc6238TotpGenerator"/> and
    /// <see cref="WebAuthnOptionsGenerator"/>: it holds the relying-party configuration and nothing else.
    /// It does not know which challenge was issued to whom, does not read or write credentials, and does
    /// not decide what a declined ceremony means. Each of those is the application's, which is what keeps
    /// the storage model, the enrollment state machine and the lockout policy out of the library.
    /// </para>
    /// <para>
    /// Sealed and concrete rather than behind an interface. Verification is a pure function of its
    /// arguments, so a test that wants a particular outcome constructs the inputs that produce it; there is
    /// nothing here a consumer needs to fake.
    /// </para>
    /// </remarks>
    public sealed class WebAuthnVerifier
    {
        private readonly WebAuthnRelyingParty _relyingParty;

        /// <summary>
        /// Creates a verifier for one relying party.
        /// </summary>
        /// <param name="relyingParty">The validated relying-party configuration.</param>
        /// <exception cref="ArgumentNullException"><paramref name="relyingParty"/> is null.</exception>
        public WebAuthnVerifier(WebAuthnRelyingParty relyingParty)
        {
            ArgumentNullException.ThrowIfNull(relyingParty);

            _relyingParty = relyingParty;
        }

        /// <summary>
        /// Verifies a registration ceremony and returns the credential to store.
        /// </summary>
        /// <param name="response">The credential the browser posted back.</param>
        /// <param name="expectedChallenge">
        /// The challenge that was issued for this ceremony — the <c>Challenge</c> of the
        /// <see cref="PublicKeyCredentialCreationOptions"/> the application stored against the session.
        /// </param>
        /// <param name="userVerification">
        /// The requirement the options were generated with. Passing a weaker value than was asked for is
        /// what would let an authenticator that skipped verification enroll under a policy that required
        /// it, so this is the same value that went into
        /// <see cref="WebAuthnOptionsGenerator.CreateRegistrationOptions"/>.
        /// </param>
        /// <returns>
        /// The outcome. Every way a real ceremony can fail is a reason on this result; nothing about a
        /// declined registration is reported as an exception.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="response"/> or <paramref name="expectedChallenge"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="userVerification"/> is not a defined value.</exception>
        /// <exception cref="ArgumentException"><paramref name="expectedChallenge"/> is not a challenge this library issued.</exception>
        public WebAuthnRegistrationResult VerifyRegistration(
            WebAuthnRegistrationResponse response,
            string expectedChallenge,
            WebAuthnUserVerificationRequirement userVerification)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentNullException.ThrowIfNull(expectedChallenge);
            EnumArgument.ThrowIfUndefined(userVerification, nameof(userVerification));

            byte[] expectedChallengeBytes = DecodeIssuedChallenge(expectedChallenge, nameof(expectedChallenge));

            try
            {
                return VerifyRegistrationOrThrow(response, expectedChallengeBytes, userVerification);
            }
            catch (WebAuthnParseException exception)
            {
                return WebAuthnRegistrationResult.Failed(WebAuthnParseErrorTranslation.ToRegistrationReason(exception.Error), exception.Message);
            }
        }

        /// <summary>
        /// Runs the registration checks in the order WebAuthn Level 3 §7.1 states them, letting the
        /// parsers report a malformed structure by throwing so each check reads as the single condition it
        /// is.
        /// </summary>
        private WebAuthnRegistrationResult VerifyRegistrationOrThrow(
            WebAuthnRegistrationResponse response,
            byte[] expectedChallengeBytes,
            WebAuthnUserVerificationRequirement userVerification)
        {
            CollectedClientData clientData = CollectedClientData.Parse(response.Response.ClientDataJsonBytes);

            if (!string.Equals(clientData.Type, CollectedClientData.RegistrationCeremonyType, StringComparison.Ordinal))
            {
                return WebAuthnRegistrationResult.Failed(
                    WebAuthnRegistrationFailureReason.WrongCeremonyType,
                    $"Client data records the ceremony \"{clientData.Type}\"; a registration is \"{CollectedClientData.RegistrationCeremonyType}\".");
            }

            if (!ChallengeMatches(clientData.Challenge, expectedChallengeBytes))
            {
                return WebAuthnRegistrationResult.Failed(
                    WebAuthnRegistrationFailureReason.ChallengeMismatch,
                    "Client data answers a different challenge than the one issued for this ceremony.");
            }

            if (!_relyingParty.IsAllowedOrigin(clientData.Origin))
            {
                return WebAuthnRegistrationResult.Failed(
                    WebAuthnRegistrationFailureReason.OriginNotAllowed,
                    $"The ceremony ran at \"{clientData.Origin}\", which is not one of the relying party's configured origins.");
            }

            if (clientData.CrossOrigin)
            {
                return WebAuthnRegistrationResult.Failed(
                    WebAuthnRegistrationFailureReason.CrossOriginCeremony,
                    "The ceremony ran inside a frame embedded by another site.");
            }

            AuthenticatorData authenticatorData = AttestationObject.Parse(response.Response.AttestationObjectBytes).AuthenticatorData;

            if (!CryptographicOperations.FixedTimeEquals(authenticatorData.RelyingPartyIdHash, _relyingParty.IdHash.Span))
            {
                return WebAuthnRegistrationResult.Failed(
                    WebAuthnRegistrationFailureReason.RelyingPartyIdHashMismatch,
                    $"The credential is scoped to a different relying-party id than \"{_relyingParty.Id}\".");
            }

            if (!authenticatorData.UserPresent)
            {
                return WebAuthnRegistrationResult.Failed(
                    WebAuthnRegistrationFailureReason.UserNotPresent,
                    "The authenticator did not report user presence, which WebAuthn requires on every registration.");
            }

            if (userVerification == WebAuthnUserVerificationRequirement.Required && !authenticatorData.UserVerified)
            {
                return WebAuthnRegistrationResult.Failed(
                    WebAuthnRegistrationFailureReason.UserNotVerified,
                    "Registration required user verification and the authenticator did not report it.");
            }

            if (authenticatorData.CredentialId == null || authenticatorData.CredentialPublicKey == null || authenticatorData.Aaguid == null)
            {
                return WebAuthnRegistrationResult.Failed(
                    WebAuthnRegistrationFailureReason.MissingAttestedCredentialData,
                    "The authenticator data carried no attested credential data, so the ceremony produced no credential to store.");
            }

            if (!response.CredentialIdBytes.AsSpan().SequenceEqual(authenticatorData.CredentialId))
            {
                return WebAuthnRegistrationResult.Failed(
                    WebAuthnRegistrationFailureReason.CredentialIdMismatch,
                    "The credential id the browser reported is not the one inside the authenticator data.");
            }

            using CoseKey publicKey = CoseKey.Parse(authenticatorData.CredentialPublicKey);

            WebAuthnRegisteredCredential credential = new WebAuthnRegisteredCredential(
                Base64UrlEncoding.Encode(authenticatorData.CredentialId),
                Base64UrlEncoding.Encode(authenticatorData.CredentialPublicKey),
                publicKey.Algorithm,
                authenticatorData.SignCount,
                authenticatorData.Aaguid.Value,
                authenticatorData.BackupEligible,
                authenticatorData.BackedUp,
                response.Response.Transports);

            return WebAuthnRegistrationResult.Succeeded(credential, authenticatorData.UserVerified);
        }

        /// <summary>
        /// Whether the challenge the browser recorded stands for the bytes that were issued.
        /// </summary>
        /// <remarks>
        /// Compared as bytes rather than as text, so a client that pads its base64url is not rejected for a
        /// spelling — the lax half of the policy <see cref="Base64UrlEncoding"/> states — and in fixed
        /// time, because the challenge is the one value in the ceremony an attacker would be trying to
        /// guess.
        /// </remarks>
        private static bool ChallengeMatches(string presentedChallenge, byte[] expectedChallengeBytes)
        {
            byte[]? presented = Base64UrlEncoding.TryDecode(presentedChallenge);

            // Text that is not base64url at all cannot be a challenge this library issued, so it is the
            // same outcome as one that decodes to the wrong bytes rather than a failure of its own.
            return presented != null && CryptographicOperations.FixedTimeEquals(presented, expectedChallengeBytes);
        }

        /// <summary>
        /// Decodes a challenge the application stored and handed back.
        /// </summary>
        /// <remarks>
        /// Strict where <see cref="ChallengeMatches"/> is lax, for the reason given there. The length is
        /// checked along with the encoding because canonicality alone does not make a value one this
        /// library issued: a stored challenge truncated in transit or in a narrow database column is still
        /// canonical base64url of something, and would turn every ceremony into a challenge mismatch —
        /// reporting an attack on each attempt instead of the storage bug behind all of them.
        /// </remarks>
        private static byte[] DecodeIssuedChallenge(string challenge, string parameterName)
        {
            byte[]? decoded = Base64UrlEncoding.TryDecodeCanonical(challenge);

            if (decoded == null || decoded.Length != WebAuthnOptionsGenerator.ChallengeSizeInBytes)
            {
                throw new ArgumentException(
                    $"'{parameterName}' is not a challenge this library issued: it must be the {WebAuthnOptionsGenerator.ChallengeSizeInBytes} bytes handed out as the options' Challenge, in the canonical base64url they were handed out in.",
                    parameterName);
            }

            return decoded;
        }
    }
}
