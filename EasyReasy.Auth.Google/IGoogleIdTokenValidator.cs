namespace EasyReasy.Auth.Google
{
    /// <summary>
    /// Validates a Google ID token and extracts user information.
    /// This is an internal testability seam wrapping Google's static validation API.
    /// </summary>
    internal interface IGoogleIdTokenValidator
    {
        /// <summary>
        /// Validates the specified Google ID token and returns the extracted user information.
        /// </summary>
        /// <param name="idToken">The Google ID token to validate.</param>
        /// <returns>The validated user information.</returns>
        /// <exception cref="GoogleTokenValidationException">Thrown when the token is invalid or validation fails.</exception>
        Task<GoogleUserInfo> ValidateAsync(string idToken);
    }
}
