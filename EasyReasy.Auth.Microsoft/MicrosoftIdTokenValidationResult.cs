using System.Diagnostics.CodeAnalysis;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The outcome of validating an ID token: the identity it carries, or why it is not valid.
    /// </summary>
    internal sealed class MicrosoftIdTokenValidationResult
    {
        private MicrosoftIdTokenValidationResult(MicrosoftUserInfo? userInfo, MicrosoftAuthFailureReason? failureReason)
        {
            UserInfo = userInfo;
            FailureReason = failureReason;
        }

        /// <summary>
        /// Whether the token is valid.
        /// </summary>
        [MemberNotNullWhen(true, nameof(UserInfo))]
        [MemberNotNullWhen(false, nameof(FailureReason))]
        public bool IsValid => FailureReason == null;

        /// <summary>
        /// The identity in a valid token.
        /// </summary>
        public MicrosoftUserInfo? UserInfo { get; }

        /// <summary>
        /// Why the token is not valid.
        /// </summary>
        public MicrosoftAuthFailureReason? FailureReason { get; }

        /// <summary>
        /// Creates the result for a valid token.
        /// </summary>
        /// <param name="userInfo">The identity it carries.</param>
        /// <returns>A valid result.</returns>
        public static MicrosoftIdTokenValidationResult Valid(MicrosoftUserInfo userInfo) => new MicrosoftIdTokenValidationResult(userInfo, null);

        /// <summary>
        /// Creates the result for a token that is not valid.
        /// </summary>
        /// <param name="reason">Why it is not.</param>
        /// <returns>An invalid result.</returns>
        public static MicrosoftIdTokenValidationResult Invalid(MicrosoftAuthFailureReason reason) => new MicrosoftIdTokenValidationResult(null, reason);
    }
}
