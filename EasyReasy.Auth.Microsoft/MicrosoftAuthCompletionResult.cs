using System.Diagnostics.CodeAnalysis;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The outcome of <see cref="IMicrosoftAuthService.CompleteAsync"/>: the validated identity and the caller binding
    /// from Begin, or the reason the flow was refused.
    /// </summary>
    public sealed class MicrosoftAuthCompletionResult
    {
        private MicrosoftAuthCompletionResult(
            MicrosoftUserInfo? userInfo,
            string? callerBinding,
            MicrosoftAuthFailureReason? failureReason,
            string? attemptedSubject)
        {
            UserInfo = userInfo;
            CallerBinding = callerBinding;
            FailureReason = failureReason;
            AttemptedSubject = attemptedSubject;
        }

        /// <summary>
        /// Whether every check passed.
        /// </summary>
        [MemberNotNullWhen(true, nameof(UserInfo), nameof(CallerBinding), nameof(AttemptedSubject))]
        [MemberNotNullWhen(false, nameof(FailureReason))]
        public bool Success => FailureReason == null;

        /// <summary>
        /// The validated identity. Only populated when <see cref="Success"/> is true.
        /// </summary>
        public MicrosoftUserInfo? UserInfo { get; }

        /// <summary>
        /// The <see cref="MicrosoftAuthBeginRequest.CallerBinding"/> the flow was begun with. Only populated when
        /// <see cref="Success"/> is true. Compare it with what the completing endpoint expects before acting on the
        /// identity.
        /// </summary>
        public string? CallerBinding { get; }

        /// <summary>
        /// Why the flow was refused. Only populated when <see cref="Success"/> is false.
        /// </summary>
        public MicrosoftAuthFailureReason? FailureReason { get; }

        /// <summary>
        /// The account as <c>tid:oid</c>, for an audit record: populated on success, and on a refusal of a genuine
        /// token (its tenant, authentication context or sign-in time). Null when the token itself, the state, the
        /// cookie or the code failed, because no trustworthy account is known then.
        /// </summary>
        public string? AttemptedSubject { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="userInfo">The validated identity.</param>
        /// <param name="callerBinding">The caller binding from Begin.</param>
        /// <returns>A successful result.</returns>
        internal static MicrosoftAuthCompletionResult Succeeded(MicrosoftUserInfo userInfo, string callerBinding)
        {
            return new MicrosoftAuthCompletionResult(userInfo, callerBinding, null, FormatSubject(userInfo));
        }

        /// <summary>
        /// Creates a refusal of a genuine token, which names the account it was for.
        /// </summary>
        /// <param name="reason">Why the account was refused.</param>
        /// <param name="userInfo">The identity in the refused token.</param>
        /// <returns>A failed result.</returns>
        internal static MicrosoftAuthCompletionResult Refused(MicrosoftAuthFailureReason reason, MicrosoftUserInfo userInfo)
        {
            return new MicrosoftAuthCompletionResult(null, null, reason, FormatSubject(userInfo));
        }

        /// <summary>
        /// Creates a failure before any trustworthy account is known.
        /// </summary>
        /// <param name="reason">Why the flow failed.</param>
        /// <returns>A failed result.</returns>
        internal static MicrosoftAuthCompletionResult Failed(MicrosoftAuthFailureReason reason)
        {
            return new MicrosoftAuthCompletionResult(null, null, reason, null);
        }

        private static string FormatSubject(MicrosoftUserInfo userInfo)
        {
            return $"{userInfo.TenantId:D}:{userInfo.ObjectId}";
        }
    }
}
