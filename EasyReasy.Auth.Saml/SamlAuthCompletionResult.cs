using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The outcome of <see cref="ISamlAuthService.CompleteAsync"/>: the verified user, or the reason the sign-in was
    /// refused.
    /// </summary>
    public sealed class SamlAuthCompletionResult
    {
        private SamlAuthCompletionResult(SamlUserInfo? userInfo, SamlAuthFailureReason? failureReason, SamlIdentity? attemptedIdentity)
        {
            UserInfo = userInfo;
            FailureReason = failureReason;
            AttemptedIdentity = attemptedIdentity;
        }

        /// <summary>
        /// Whether every check passed.
        /// </summary>
        [MemberNotNullWhen(true, nameof(UserInfo), nameof(AttemptedIdentity))]
        [MemberNotNullWhen(false, nameof(FailureReason))]
        public bool Success => FailureReason == null;

        /// <summary>
        /// The verified user. Only populated when <see cref="Success"/> is true. Personal data, so it is left out of
        /// <see cref="ToString"/> and of JSON serialization.
        /// </summary>
        [JsonIgnore]
        public SamlUserInfo? UserInfo { get; }

        /// <summary>
        /// Why the sign-in was refused. Only populated when <see cref="Success"/> is false.
        /// </summary>
        public SamlAuthFailureReason? FailureReason { get; }

        /// <summary>
        /// The account, for an audit record: populated on success, and on a refusal of a verified assertion (its account,
        /// authentication time or class). Null when the code, the cookie, the identity provider or the response failed, or
        /// the assertion carried no identity, because no trustworthy account is known then.
        /// </summary>
        public SamlIdentity? AttemptedIdentity { get; }

        internal static SamlAuthCompletionResult Succeeded(SamlUserInfo userInfo)
        {
            return new SamlAuthCompletionResult(userInfo, null, userInfo.Identity);
        }

        internal static SamlAuthCompletionResult Refused(SamlAuthFailureReason reason, SamlIdentity? attemptedIdentity)
        {
            return new SamlAuthCompletionResult(null, reason, attemptedIdentity);
        }

        /// <summary>
        /// Describes the outcome without the user's details, so the result is safe to log. JSON serialization leaves them
        /// out too.
        /// </summary>
        /// <returns>The outcome, the failure reason and the account when they are known.</returns>
        public override string ToString()
        {
            List<string> parts = new List<string> { $"{nameof(Success)} = {(Success ? "true" : "false")}" };

            if (FailureReason is SamlAuthFailureReason reason)
            {
                parts.Add($"{nameof(FailureReason)} = {reason}");
            }

            if (AttemptedIdentity is SamlIdentity identity)
            {
                parts.Add($"{nameof(AttemptedIdentity)} = {identity}");
            }

            return $"{nameof(SamlAuthCompletionResult)} {{ {string.Join(", ", parts)} }}";
        }
    }
}
