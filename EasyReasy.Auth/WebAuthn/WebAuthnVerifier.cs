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
                return WebAuthnRegistrationResult.Failed(WebAuthnFailureTranslation.ToRegistrationReason(exception.Error), exception.Message);
            }
        }

        /// <summary>
        /// Runs the registration checks in the order WebAuthn Level 3 §7.1 states them, letting the
        /// parsers report a malformed structure by throwing so each check reads as the single condition it
        /// is. The checks shared with §7.2 are <see cref="WebAuthnCeremonyChecks"/>'; what is left here is
        /// what only a registration does.
        /// </summary>
        private WebAuthnRegistrationResult VerifyRegistrationOrThrow(
            WebAuthnRegistrationResponse response,
            byte[] expectedChallengeBytes,
            WebAuthnUserVerificationRequirement userVerification)
        {
            CollectedClientData clientData = CollectedClientData.Parse(response.Response.ClientDataJsonBytes);

            WebAuthnCeremonyFailure? clientDataFailure = WebAuthnCeremonyChecks.CheckClientData(
                clientData,
                CollectedClientData.RegistrationCeremonyType,
                expectedChallengeBytes,
                _relyingParty);

            if (clientDataFailure != null)
            {
                return WebAuthnRegistrationResult.Failed(
                    WebAuthnFailureTranslation.ToRegistrationReason(clientDataFailure.Check),
                    clientDataFailure.Message);
            }

            AuthenticatorData authenticatorData = AttestationObject.Parse(response.Response.AttestationObjectBytes).AuthenticatorData;

            WebAuthnCeremonyFailure? authenticatorDataFailure = WebAuthnCeremonyChecks.CheckAuthenticatorData(
                authenticatorData,
                _relyingParty,
                userVerification);

            if (authenticatorDataFailure != null)
            {
                return WebAuthnRegistrationResult.Failed(
                    WebAuthnFailureTranslation.ToRegistrationReason(authenticatorDataFailure.Check),
                    authenticatorDataFailure.Message);
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
        /// Verifies an authentication ceremony against the credential the application looked up.
        /// </summary>
        /// <param name="response">The assertion the browser posted back.</param>
        /// <param name="expectedChallenge">
        /// The challenge that was issued for this ceremony — the <c>Challenge</c> of the
        /// <see cref="PublicKeyCredentialRequestOptions"/> the application stored against the session.
        /// </param>
        /// <param name="storedCredential">
        /// The credential registration reported, looked up by <see cref="WebAuthnAuthenticationResponse.Id"/>.
        /// Which credentials it is looked up among is what ties the two factors to one person, and it is
        /// the application's step — see <see cref="WebAuthnStoredCredential"/>, which states the obligation
        /// in full.
        /// </param>
        /// <param name="userVerification">
        /// The requirement the options were generated with. Passing a weaker value than was asked for is
        /// what would let an authenticator that skipped verification authenticate under a policy that
        /// required it, so this is the same value that went into
        /// <see cref="WebAuthnOptionsGenerator.CreateAuthenticationOptions"/>.
        /// </param>
        /// <returns>
        /// The outcome, carrying the sign counter to store when it succeeded. Every way a real ceremony can
        /// fail is a reason on this result; nothing about a declined assertion is reported as an exception.
        /// </returns>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="userVerification"/> is not a defined value.</exception>
        /// <exception cref="ArgumentException"><paramref name="expectedChallenge"/> is not a challenge this library issued.</exception>
        public WebAuthnAuthenticationResult VerifyAuthentication(
            WebAuthnAuthenticationResponse response,
            string expectedChallenge,
            WebAuthnStoredCredential storedCredential,
            WebAuthnUserVerificationRequirement userVerification)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentNullException.ThrowIfNull(expectedChallenge);
            ArgumentNullException.ThrowIfNull(storedCredential);
            EnumArgument.ThrowIfUndefined(userVerification, nameof(userVerification));

            byte[] expectedChallengeBytes = DecodeIssuedChallenge(expectedChallenge, nameof(expectedChallenge));

            try
            {
                return VerifyAuthenticationOrThrow(response, expectedChallengeBytes, storedCredential, userVerification);
            }
            catch (WebAuthnParseException exception)
            {
                return WebAuthnAuthenticationResult.Failed(WebAuthnFailureTranslation.ToAuthenticationReason(exception.Error), exception.Message);
            }
        }

        /// <summary>
        /// Runs the authentication checks in the order WebAuthn Level 3 §7.2 states them, letting the
        /// parsers report a malformed structure by throwing so each check reads as the single condition it
        /// is. The checks shared with §7.1 are <see cref="WebAuthnCeremonyChecks"/>'; what is left here is
        /// what only an assertion does.
        /// </summary>
        private WebAuthnAuthenticationResult VerifyAuthenticationOrThrow(
            WebAuthnAuthenticationResponse response,
            byte[] expectedChallengeBytes,
            WebAuthnStoredCredential storedCredential,
            WebAuthnUserVerificationRequirement userVerification)
        {
            if (!response.CredentialIdBytes.AsSpan().SequenceEqual(storedCredential.CredentialIdBytes))
            {
                // Caught before anything else is read: verifying an assertion against a credential it does
                // not name would answer a question nobody asked, and the signature check would fail in a
                // way that reads as an attack rather than as a lookup that returned the wrong row.
                return WebAuthnAuthenticationResult.Failed(
                    WebAuthnAuthenticationFailureReason.CredentialIdMismatch,
                    "The assertion names a different credential than the one it was given to verify against.");
            }

            CollectedClientData clientData = CollectedClientData.Parse(response.Response.ClientDataJsonBytes);

            WebAuthnCeremonyFailure? clientDataFailure = WebAuthnCeremonyChecks.CheckClientData(
                clientData,
                CollectedClientData.AuthenticationCeremonyType,
                expectedChallengeBytes,
                _relyingParty);

            if (clientDataFailure != null)
            {
                return WebAuthnAuthenticationResult.Failed(
                    WebAuthnFailureTranslation.ToAuthenticationReason(clientDataFailure.Check),
                    clientDataFailure.Message);
            }

            AuthenticatorData authenticatorData = AuthenticatorData.Parse(response.Response.AuthenticatorDataBytes);

            WebAuthnCeremonyFailure? authenticatorDataFailure = WebAuthnCeremonyChecks.CheckAuthenticatorData(
                authenticatorData,
                _relyingParty,
                userVerification);

            if (authenticatorDataFailure != null)
            {
                return WebAuthnAuthenticationResult.Failed(
                    WebAuthnFailureTranslation.ToAuthenticationReason(authenticatorDataFailure.Check),
                    authenticatorDataFailure.Message);
            }

            if (!SignatureIsValid(response, storedCredential))
            {
                return WebAuthnAuthenticationResult.Failed(
                    WebAuthnAuthenticationFailureReason.SignatureInvalid,
                    "The signature does not verify against the stored public key.");
            }

            WebAuthnSignCounterState? signCounterState = CheckSignCounter(storedCredential.SignCount, authenticatorData.SignCount);

            if (signCounterState == null)
            {
                return WebAuthnAuthenticationResult.Failed(
                    WebAuthnAuthenticationFailureReason.SignCounterRegressed,
                    $"The authenticator's signature counter did not advance: it reported {authenticatorData.SignCount} against a stored {storedCredential.SignCount}.");
            }

            return WebAuthnAuthenticationResult.Succeeded(
                authenticatorData.SignCount,
                signCounterState.Value,
                authenticatorData.UserVerified,
                authenticatorData.BackedUp);
        }

        /// <summary>
        /// Verifies the signature over the authenticator data followed by the hash of the client data.
        /// </summary>
        /// <remarks>
        /// The signed bytes are assembled from what was received rather than from anything re-serialized:
        /// the authenticator signed the exact bytes the browser sent, so a client data object rebuilt from
        /// its parsed fields would hash to something else even when every field matched.
        /// </remarks>
        private static bool SignatureIsValid(WebAuthnAuthenticationResponse response, WebAuthnStoredCredential storedCredential)
        {
            byte[] authenticatorDataBytes = response.Response.AuthenticatorDataBytes;
            byte[] clientDataHash = SHA256.HashData(response.Response.ClientDataJsonBytes);
            byte[] signedBytes = new byte[authenticatorDataBytes.Length + clientDataHash.Length];

            authenticatorDataBytes.CopyTo(signedBytes, 0);
            clientDataHash.CopyTo(signedBytes, authenticatorDataBytes.Length);

            using CoseKey publicKey = CoseKey.Parse(storedCredential.PublicKeyBytes);
            return publicKey.VerifySignature(signedBytes, response.Response.SignatureBytes);
        }

        /// <summary>
        /// Applies WebAuthn's signature-counter rule, reporting what the counter said.
        /// </summary>
        /// <remarks>
        /// Not a strict monotonic comparison, and this is the part of the ceremony most easily got wrong.
        /// An authenticator that keeps no counter reports 0 on every assertion — Touch ID and Face ID among
        /// them, which is the factor most users of this feature will have — so requiring the count to
        /// advance would reject every one of their assertions. WebAuthn Level 3 §7.2 runs the comparison
        /// only when one of the two counts is nonzero — which is to say it skips the check when the stored
        /// and reported counts are both zero, and that is the case this returns
        /// <see cref="WebAuthnSignCounterState.NotSupported"/> for. Everywhere else the count must advance:
        /// a counter that stalls or goes backwards on an authenticator that keeps one is the signal of a
        /// cloned credential.
        /// </remarks>
        /// <param name="storedSignCount">The counter recorded against the credential.</param>
        /// <param name="presentedSignCount">The counter the assertion reported.</param>
        /// <returns>
        /// What the counter said, or null when it should have advanced and did not. Null rather than a
        /// state of its own: a regression is a rejected ceremony, and the states are what a
        /// <i>verified</i> one reports.
        /// </returns>
        private static WebAuthnSignCounterState? CheckSignCounter(uint storedSignCount, uint presentedSignCount)
        {
            if (storedSignCount == 0 && presentedSignCount == 0)
            {
                return WebAuthnSignCounterState.NotSupported;
            }

            if (presentedSignCount <= storedSignCount)
            {
                return null;
            }

            return WebAuthnSignCounterState.Advanced;
        }

        /// <summary>
        /// Decodes a challenge the application stored and handed back.
        /// </summary>
        /// <remarks>
        /// Strict where <see cref="WebAuthnCeremonyChecks"/> is lax about the challenge the browser
        /// recorded, for the reason given there — this is a value the application stored. The length is
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
