using Google.Apis.Auth;

namespace EasyReasy.Auth.Google
{
    /// <summary>
    /// Default implementation of <see cref="IGoogleIdTokenValidator"/> that uses
    /// Google's <see cref="GoogleJsonWebSignature"/> to validate ID tokens.
    /// </summary>
    internal class GoogleIdTokenValidator : IGoogleIdTokenValidator
    {
        private readonly GoogleAuthOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleIdTokenValidator"/> class.
        /// </summary>
        /// <param name="options">The Google authentication options containing the client ID and allowed domains.</param>
        public GoogleIdTokenValidator(GoogleAuthOptions options)
        {
            _options = options;
        }

        /// <inheritdoc />
        public async Task<GoogleUserInfo> ValidateAsync(string idToken)
        {
            GoogleJsonWebSignature.ValidationSettings validationSettings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new string[] { _options.ClientId },
            };

            GoogleJsonWebSignature.Payload payload;

            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings);
            }
            catch (InvalidJwtException exception)
            {
                throw new GoogleTokenValidationException("Google ID token validation failed.", exception);
            }

            return ValidatePolicyAndBuildUserInfo(payload, _options);
        }

        /// <summary>
        /// Applies the post-validation policy (hosted-domain allowlist and email-verification requirement)
        /// to an already-validated Google payload and maps it to a <see cref="GoogleUserInfo"/>. Kept separate
        /// from the network-bound token validation so the policy is unit-testable in isolation.
        /// </summary>
        /// <param name="payload">The validated Google ID token payload.</param>
        /// <param name="options">The Google authentication options.</param>
        /// <returns>The mapped <see cref="GoogleUserInfo"/>.</returns>
        /// <exception cref="GoogleTokenValidationException">
        /// Thrown when the hosted domain is not in the allowlist, or when
        /// <see cref="GoogleAuthOptions.RequireVerifiedEmail"/> is set and the account's email is not verified.
        /// </exception>
        internal static GoogleUserInfo ValidatePolicyAndBuildUserInfo(GoogleJsonWebSignature.Payload payload, GoogleAuthOptions options)
        {
            if (options.AllowedHostedDomains != null && options.AllowedHostedDomains.Count > 0)
            {
                if (payload.HostedDomain == null
                    || !options.AllowedHostedDomains.Any(domain => string.Equals(domain, payload.HostedDomain, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new GoogleTokenValidationException(
                        $"The hosted domain '{payload.HostedDomain}' is not in the list of allowed hosted domains.");
                }
            }

            if (string.IsNullOrEmpty(payload.Email))
            {
                throw new GoogleTokenValidationException("The Google ID token does not contain an email address.");
            }

            if (options.RequireVerifiedEmail && !payload.EmailVerified)
            {
                throw new GoogleTokenValidationException("The Google account's email address is not verified.");
            }

            return new GoogleUserInfo
            {
                Subject = payload.Subject,
                Email = payload.Email,
                EmailVerified = payload.EmailVerified,
                Name = payload.Name,
                PictureUrl = payload.Picture,
            };
        }
    }
}
