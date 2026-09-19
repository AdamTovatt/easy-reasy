using System.Security.Cryptography;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The checks WebAuthn Level 3 states for both ceremonies, in the order it states them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registration (§7.1) and authentication (§7.2) differ in what they produce and in the structures
    /// carrying the authenticator data, but the checks over the client data and over the authenticator
    /// data itself are the same checks in the same order. They are written here once. Two copies would be
    /// two places to fix a mistake, and a registration path hardened against something the authentication
    /// path still accepts is the shape most WebAuthn bugs take.
    /// </para>
    /// <para>
    /// Each method returns the first check that failed, or null when every one passed, and never throws:
    /// the structures are already parsed by the time they arrive here.
    /// </para>
    /// </remarks>
    internal static class WebAuthnCeremonyChecks
    {
        /// <summary>
        /// Checks what the browser recorded about the ceremony it ran.
        /// </summary>
        /// <param name="clientData">The parsed collected client data.</param>
        /// <param name="expectedCeremonyType">The ceremony being verified, as client data spells it.</param>
        /// <param name="expectedChallengeBytes">The challenge that was issued.</param>
        /// <param name="relyingParty">The relying party the ceremony must have run for.</param>
        /// <returns>The first check that failed, or null.</returns>
        public static WebAuthnCeremonyFailure? CheckClientData(
            CollectedClientData clientData,
            string expectedCeremonyType,
            byte[] expectedChallengeBytes,
            WebAuthnRelyingParty relyingParty)
        {
            if (!string.Equals(clientData.Type, expectedCeremonyType, StringComparison.Ordinal))
            {
                return new WebAuthnCeremonyFailure(
                    WebAuthnCeremonyCheck.WrongCeremonyType,
                    $"Client data records the ceremony \"{clientData.Type}\"; the one being verified is \"{expectedCeremonyType}\".");
            }

            if (!ChallengeMatches(clientData.Challenge, expectedChallengeBytes))
            {
                return new WebAuthnCeremonyFailure(
                    WebAuthnCeremonyCheck.ChallengeMismatch,
                    "Client data answers a different challenge than the one issued for this ceremony.");
            }

            if (!relyingParty.IsAllowedOrigin(clientData.Origin))
            {
                return new WebAuthnCeremonyFailure(
                    WebAuthnCeremonyCheck.OriginNotAllowed,
                    $"The ceremony ran at \"{clientData.Origin}\", which is not one of the relying party's configured origins.");
            }

            if (clientData.CrossOrigin)
            {
                return new WebAuthnCeremonyFailure(
                    WebAuthnCeremonyCheck.CrossOriginCeremony,
                    "The ceremony ran inside a frame embedded by another site.");
            }

            return null;
        }

        /// <summary>
        /// Checks what the authenticator reported about itself and about the user.
        /// </summary>
        /// <param name="authenticatorData">The parsed authenticator data.</param>
        /// <param name="relyingParty">The relying party the authenticator must have answered for.</param>
        /// <param name="userVerification">The requirement the ceremony's options were generated with.</param>
        /// <returns>The first check that failed, or null.</returns>
        public static WebAuthnCeremonyFailure? CheckAuthenticatorData(
            AuthenticatorData authenticatorData,
            WebAuthnRelyingParty relyingParty,
            WebAuthnUserVerificationRequirement userVerification)
        {
            if (!CryptographicOperations.FixedTimeEquals(authenticatorData.RelyingPartyIdHash, relyingParty.IdHash.Span))
            {
                return new WebAuthnCeremonyFailure(
                    WebAuthnCeremonyCheck.RelyingPartyIdHashMismatch,
                    $"The authenticator data is scoped to a different relying-party id than \"{relyingParty.Id}\".");
            }

            if (!authenticatorData.UserPresent)
            {
                return new WebAuthnCeremonyFailure(
                    WebAuthnCeremonyCheck.UserNotPresent,
                    "The authenticator did not report user presence, which WebAuthn requires on every ceremony.");
            }

            if (userVerification == WebAuthnUserVerificationRequirement.Required && !authenticatorData.UserVerified)
            {
                return new WebAuthnCeremonyFailure(
                    WebAuthnCeremonyCheck.UserNotVerified,
                    "The ceremony was requested with user verification required and the authenticator did not report it.");
            }

            return null;
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
    }
}
