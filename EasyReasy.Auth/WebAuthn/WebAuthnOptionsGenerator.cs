using System.Security.Cryptography;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Produces the options a browser needs to run a WebAuthn ceremony, including the challenge each one
    /// must answer.
    /// </summary>
    /// <remarks>
    /// Pure and stateless, like the TOTP primitives: it generates a challenge and returns it, and
    /// <b>storing</b> that challenge against the session and expiring it is the application's job, exactly
    /// as replay protection is for TOTP. A challenge this library has handed out and forgotten is what makes
    /// the ceremony fresh; keeping track of which one was issued to whom is policy, and policy stays in the
    /// application.
    /// </remarks>
    public sealed class WebAuthnOptionsGenerator
    {
        /// <summary>
        /// Challenge length in bytes. WebAuthn requires at least 16; 32 matches the output of the SHA-256
        /// the ceremony is built around and leaves no question of whether the minimum was met.
        /// </summary>
        public const int ChallengeSizeInBytes = 32;

        /// <summary>Default time the browser waits for the user, in milliseconds.</summary>
        public const int DefaultTimeoutMilliseconds = 60_000;

        /// <summary>
        /// The algorithms offered at registration, most preferred first. Derived from
        /// <see cref="CoseAlgorithm"/> so the offer cannot come to name an algorithm verification would
        /// then refuse.
        /// </summary>
        /// <remarks>
        /// Wrapped rather than left as an array, because the array is handed to every options object this
        /// generator produces: a caller who cast the list back could rewrite the algorithms every later
        /// registration offers.
        /// </remarks>
        private static readonly IReadOnlyList<PublicKeyCredentialParameters> AcceptedAlgorithms =
            Array.AsReadOnly(new[]
            {
                new PublicKeyCredentialParameters(CoseAlgorithm.Es256),
                new PublicKeyCredentialParameters(CoseAlgorithm.Rs256),
            });

        private readonly PublicKeyCredentialRpEntity _relyingPartyEntity;

        /// <summary>
        /// Creates a generator for one relying party.
        /// </summary>
        /// <param name="relyingParty">The validated relying-party configuration.</param>
        /// <exception cref="ArgumentNullException"><paramref name="relyingParty"/> is null.</exception>
        public WebAuthnOptionsGenerator(WebAuthnRelyingParty relyingParty)
        {
            ArgumentNullException.ThrowIfNull(relyingParty);

            _relyingPartyEntity = new PublicKeyCredentialRpEntity(relyingParty);
        }

        /// <summary>
        /// Creates the options for a registration ceremony, with a fresh challenge.
        /// </summary>
        /// <param name="user">The user the credential is being registered for.</param>
        /// <param name="userVerification">
        /// How strongly the authenticator is asked to verify the user. Deliberately without a default: what
        /// this factor is worth as evidence depends on the answer, so the application states it rather than
        /// inheriting one.
        /// </param>
        /// <param name="excludeCredentialIds">
        /// The base64url credential ids this user has already registered, so the browser refuses to enroll
        /// the same authenticator twice. Null or empty on a first enrollment.
        /// </param>
        /// <param name="authenticatorAttachment">
        /// Which kind of authenticator to steer the user towards, or null to leave the choice between a
        /// built-in authenticator and a security key to them.
        /// </param>
        /// <param name="timeoutMilliseconds">How long the browser should wait for the user.</param>
        /// <returns>The options to send to the page, whose <c>Challenge</c> the caller must store.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="user"/>, or an element of <paramref name="excludeCredentialIds"/>, is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeoutMilliseconds"/> is not positive, or an enum argument is not a defined value.</exception>
        /// <exception cref="ArgumentException">A credential id is empty or is not canonical base64url.</exception>
        public PublicKeyCredentialCreationOptions CreateRegistrationOptions(
            PublicKeyCredentialUserEntity user,
            WebAuthnUserVerificationRequirement userVerification,
            IEnumerable<string>? excludeCredentialIds = null,
            WebAuthnAuthenticatorAttachment? authenticatorAttachment = null,
            int timeoutMilliseconds = DefaultTimeoutMilliseconds)
        {
            ArgumentNullException.ThrowIfNull(user);
            ThrowIfTimeoutIsNotPositive(timeoutMilliseconds);
            ThrowIfUndefined(userVerification, nameof(userVerification));

            if (authenticatorAttachment != null)
            {
                ThrowIfUndefined(authenticatorAttachment.Value, nameof(authenticatorAttachment));
            }

            IReadOnlyList<PublicKeyCredentialDescriptor>? excludeCredentials = null;
            if (excludeCredentialIds != null)
            {
                List<PublicKeyCredentialDescriptor> descriptors = ToDescriptors(excludeCredentialIds, nameof(excludeCredentialIds));
                // Left null rather than empty so the field is omitted: an empty excludeCredentials means
                // the same thing as an absent one, and omitting it keeps the JSON to what it states.
                excludeCredentials = descriptors.Count == 0 ? null : descriptors;
            }

            return new PublicKeyCredentialCreationOptions(
                _relyingPartyEntity,
                user,
                CreateChallenge(),
                AcceptedAlgorithms,
                timeoutMilliseconds,
                excludeCredentials,
                new AuthenticatorSelectionCriteria(userVerification, authenticatorAttachment));
        }

        /// <summary>
        /// Creates the options for an authentication ceremony, with a fresh challenge.
        /// </summary>
        /// <param name="allowCredentialIds">
        /// The base64url credential ids this user may answer with — what the application looked up for the
        /// user who has just passed their first factor. At least one is required: an empty list asks for any
        /// discoverable credential, which is passwordless login and out of scope for a second factor.
        /// </param>
        /// <param name="userVerification">How strongly the authenticator is asked to verify the user.</param>
        /// <param name="timeoutMilliseconds">How long the browser should wait for the user.</param>
        /// <returns>The options to send to the page, whose <c>Challenge</c> the caller must store.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="allowCredentialIds"/>, or an element of it, is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeoutMilliseconds"/> is not positive, or <paramref name="userVerification"/> is not a defined value.</exception>
        /// <exception cref="ArgumentException"><paramref name="allowCredentialIds"/> is empty, or a credential id is empty or is not canonical base64url.</exception>
        public PublicKeyCredentialRequestOptions CreateAuthenticationOptions(
            IEnumerable<string> allowCredentialIds,
            WebAuthnUserVerificationRequirement userVerification,
            int timeoutMilliseconds = DefaultTimeoutMilliseconds)
        {
            ArgumentNullException.ThrowIfNull(allowCredentialIds);
            ThrowIfTimeoutIsNotPositive(timeoutMilliseconds);
            ThrowIfUndefined(userVerification, nameof(userVerification));

            List<PublicKeyCredentialDescriptor> allowCredentials = ToDescriptors(allowCredentialIds, nameof(allowCredentialIds));
            if (allowCredentials.Count == 0)
            {
                throw new ArgumentException(
                    $"'{nameof(allowCredentialIds)}' must name at least one registered credential; an empty list asks the browser for any discoverable credential, which this library does not verify.",
                    nameof(allowCredentialIds));
            }

            return new PublicKeyCredentialRequestOptions(
                CreateChallenge(),
                timeoutMilliseconds,
                _relyingPartyEntity.Id,
                allowCredentials,
                userVerification);
        }

        /// <summary>
        /// Generates a fresh challenge.
        /// </summary>
        private static string CreateChallenge()
        {
            return Base64UrlEncoding.Encode(RandomNumberGenerator.GetBytes(ChallengeSizeInBytes));
        }

        /// <summary>
        /// Validates each stored credential id and wraps it in a descriptor. Validation lives here rather
        /// than in the descriptor so the exception names the argument the application passed.
        /// </summary>
        private static List<PublicKeyCredentialDescriptor> ToDescriptors(IEnumerable<string> credentialIds, string parameterName)
        {
            List<PublicKeyCredentialDescriptor> descriptors = new List<PublicKeyCredentialDescriptor>();

            foreach (string credentialId in credentialIds)
            {
                if (credentialId == null)
                {
                    throw new ArgumentNullException(parameterName, $"'{parameterName}' contains a null credential id.");
                }

                if (credentialId.Length == 0)
                {
                    throw new ArgumentException($"'{parameterName}' contains an empty credential id.", parameterName);
                }

                if (!Base64UrlEncoding.IsCanonical(credentialId))
                {
                    // Caught here rather than at the browser, which answers a credential id it cannot match
                    // with a ceremony that simply fails, saying nothing about why. Padding and whitespace
                    // are rejected along with the wrong alphabet: the contract's Base64URLString is
                    // unpadded, so forwarding a padded id verbatim would move the failure rather than avoid it.
                    throw new ArgumentException(
                        $"'{parameterName}' contains \"{credentialId}\", which is not canonical base64url — the unpadded, whitespace-free form this library reports a credential id in.",
                        parameterName);
                }

                descriptors.Add(new PublicKeyCredentialDescriptor(credentialId));
            }

            return descriptors;
        }

        /// <summary>
        /// Rejects a timeout a browser would not act on.
        /// </summary>
        private static void ThrowIfTimeoutIsNotPositive(int timeoutMilliseconds)
        {
            if (timeoutMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), timeoutMilliseconds, "A ceremony timeout must be positive.");
            }
        }

        /// <summary>
        /// Rejects an enum value outside the declared members. The string enum converter writes an
        /// undefined value as a number rather than failing, so without this the browser would be handed
        /// options carrying <c>"userVerification":99</c> and nothing here would have complained.
        /// </summary>
        private static void ThrowIfUndefined<TEnum>(TEnum value, string parameterName)
            where TEnum : struct, Enum
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, $"'{parameterName}' is not a defined {typeof(TEnum).Name} value.");
            }
        }
    }
}
