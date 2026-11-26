using System.Diagnostics.CodeAnalysis;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The outcome of validating an ID token: the identity it carries and the key it was verified with, or why it is not
    /// valid.
    /// </summary>
    internal sealed class OpenIdConnectIdTokenValidationResult
    {
        private OpenIdConnectIdTokenValidationResult(OpenIdConnectUserInfo? userInfo, string? signingKey, OpenIdConnectAuthFailureReason? failureReason)
        {
            UserInfo = userInfo;
            SigningKey = signingKey;
            FailureReason = failureReason;
        }

        /// <summary>
        /// Whether the token is valid.
        /// </summary>
        [MemberNotNullWhen(true, nameof(UserInfo), nameof(SigningKey))]
        [MemberNotNullWhen(false, nameof(FailureReason))]
        public bool IsValid => FailureReason == null;

        /// <summary>
        /// The identity in a valid token.
        /// </summary>
        public OpenIdConnectUserInfo? UserInfo { get; }

        /// <summary>
        /// The public key a valid token's signature verified with, as JWK JSON.
        /// </summary>
        public string? SigningKey { get; }

        /// <summary>
        /// Why the token is not valid.
        /// </summary>
        public OpenIdConnectAuthFailureReason? FailureReason { get; }

        /// <summary>
        /// Creates the result for a valid token.
        /// </summary>
        /// <param name="userInfo">The identity it carries.</param>
        /// <param name="signingKey">The public key it verified with, as JWK JSON.</param>
        /// <returns>A valid result.</returns>
        public static OpenIdConnectIdTokenValidationResult Valid(OpenIdConnectUserInfo userInfo, string signingKey) => new OpenIdConnectIdTokenValidationResult(userInfo, signingKey, null);

        /// <summary>
        /// Creates the result for a token that is not valid.
        /// </summary>
        /// <param name="reason">Why it is not.</param>
        /// <returns>An invalid result.</returns>
        public static OpenIdConnectIdTokenValidationResult Invalid(OpenIdConnectAuthFailureReason reason) => new OpenIdConnectIdTokenValidationResult(null, null, reason);
    }
}
