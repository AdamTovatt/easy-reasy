using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The outcome of <see cref="ISamlAuthService.CompleteAsync"/>: the verified user and the caller binding from Begin, or
    /// the reason the sign-in was refused.
    /// </summary>
    public sealed class SamlAuthCompletionResult
    {
        private SamlAuthCompletionResult(SamlUserInfo? userInfo, string? callerBinding, SamlAuthFailureReason? failureReason, SamlIdentity? attemptedIdentity)
        {
            UserInfo = userInfo;
            CallerBinding = callerBinding;
            FailureReason = failureReason;
            AttemptedIdentity = attemptedIdentity;
        }

        /// <summary>
        /// Whether every check passed.
        /// </summary>
        [MemberNotNullWhen(true, nameof(UserInfo), nameof(CallerBinding), nameof(AttemptedIdentity))]
        [MemberNotNullWhen(false, nameof(FailureReason))]
        public bool Success => FailureReason == null;

        /// <summary>
        /// The verified user. Only populated when <see cref="Success"/> is true. Personal data, so it is left out of
        /// <see cref="ToString"/> and of JSON serialization.
        /// </summary>
        [JsonIgnore]
        public SamlUserInfo? UserInfo { get; }

        /// <summary>
        /// The <see cref="SamlAuthBeginRequest.CallerBinding"/> the sign-in was begun with. Only populated when
        /// <see cref="Success"/> is true. Compare it with what the completing endpoint expects before acting on the user.
        /// The application's own value, so it is left out of <see cref="ToString"/> and of JSON serialization.
        /// </summary>
        [JsonIgnore]
        public string? CallerBinding { get; }

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

        internal static SamlAuthCompletionResult Succeeded(SamlUserInfo userInfo, string callerBinding)
        {
            return new SamlAuthCompletionResult(userInfo, callerBinding, null, userInfo.Identity);
        }

        internal static SamlAuthCompletionResult Refused(SamlAuthFailureReason reason, SamlIdentity? attemptedIdentity)
        {
            return new SamlAuthCompletionResult(null, null, reason, attemptedIdentity);
        }

        /// <summary>
        /// Describes the outcome without the caller binding or the user's details, so the result is safe to log. JSON
        /// serialization leaves out the same two.
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
