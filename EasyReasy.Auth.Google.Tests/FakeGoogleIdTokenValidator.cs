using EasyReasy.Auth.Google;

namespace EasyReasy.Auth.Google.Tests
{
    /// <summary>
    /// Fake implementation of <see cref="IGoogleIdTokenValidator"/> for testing.
    /// Returns a pre-configured <see cref="GoogleUserInfo"/> or throws on validation.
    /// </summary>
    internal class FakeGoogleIdTokenValidator : IGoogleIdTokenValidator
    {
        /// <summary>
        /// The <see cref="GoogleUserInfo"/> to return when validation succeeds.
        /// Set to null and set <see cref="ExceptionToThrow"/> to simulate validation failure.
        /// </summary>
        public GoogleUserInfo? UserInfoToReturn { get; set; }

        /// <summary>
        /// The exception to throw when <see cref="ValidateAsync"/> is called.
        /// When set, the validator will throw this exception instead of returning a result.
        /// </summary>
        public GoogleTokenValidationException? ExceptionToThrow { get; set; }

        /// <summary>
        /// The last ID token that was passed to <see cref="ValidateAsync"/>.
        /// </summary>
        public string? LastValidatedToken { get; private set; }

        /// <inheritdoc />
        public Task<GoogleUserInfo> ValidateAsync(string idToken)
        {
            LastValidatedToken = idToken;

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            if (UserInfoToReturn == null)
            {
                throw new InvalidOperationException("FakeGoogleIdTokenValidator: UserInfoToReturn is not set.");
            }

            return Task.FromResult(UserInfoToReturn);
        }
    }
}
