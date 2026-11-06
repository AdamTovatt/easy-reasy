using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The outcome of <see cref="IMicrosoftAuthService.CompleteAsync"/>: the validated identity, the caller binding
    /// from Begin and the ID token it was read from, or the reason the flow was refused.
    /// </summary>
    public sealed class MicrosoftAuthCompletionResult
    {
        private MicrosoftAuthCompletionResult(
            MicrosoftUserInfo? userInfo,
            string? callerBinding,
            string? idToken,
            MicrosoftAuthFailureReason? failureReason,
            string? attemptedSubject)
        {
            UserInfo = userInfo;
            CallerBinding = callerBinding;
            IdToken = idToken;
            FailureReason = failureReason;
            AttemptedSubject = attemptedSubject;
        }

        /// <summary>
        /// Whether every check passed.
        /// </summary>
        [MemberNotNullWhen(true, nameof(UserInfo), nameof(CallerBinding), nameof(IdToken), nameof(AttemptedSubject))]
        [MemberNotNullWhen(false, nameof(FailureReason))]
        public bool Success => FailureReason == null;

        /// <summary>
        /// The validated identity. Only populated when <see cref="Success"/> is true. Personal data, so it is left out
        /// of <see cref="ToString"/> and of JSON serialization.
        /// </summary>
        [JsonIgnore]
        public MicrosoftUserInfo? UserInfo { get; }

        /// <summary>
        /// The <see cref="MicrosoftAuthBeginRequest.CallerBinding"/> the flow was begun with. Only populated when
        /// <see cref="Success"/> is true. Compare it with what the completing endpoint expects before acting on the
        /// identity. The application's own value, which may name a user or a record, so it is left out of
        /// <see cref="ToString"/> and of JSON serialization.
        /// </summary>
        [JsonIgnore]
        public string? CallerBinding { get; }

        /// <summary>
        /// The raw ID token <see cref="UserInfo"/> was read from, exactly as Microsoft's token endpoint returned it and
        /// after every check passed. Only populated when <see cref="Success"/> is true.
        /// </summary>
        /// <remarks>
        /// Evidence for a record signed through a re-check: Microsoft's own signature over the account, the sign-in
        /// time, the authentication contexts and the nonce, which anyone with Microsoft's public key for it can verify
        /// later. Seal it with the record it vouches for (encrypted, with access to it audited); never log it
        /// or send it to a browser. It is a bearer credential for this app's client id until it expires, so treat it
        /// as a secret. <see cref="ToString"/> shows it as <c>[REDACTED]</c>, JSON serialization leaves it out, and the
        /// sign-in endpoints never pass it on: neither <see cref="IMicrosoftAuthHandler"/> nor
        /// <see cref="ExternalAuthResult"/> carries it. To check it later, keep a copy of the public key it names (its
        /// <c>kid</c>) along with it, since Microsoft rotates and retires its keys: that key is the one with this
        /// <c>kid</c> in the key set at the <c>jwks_uri</c> of the OpenID configuration this package reads (see the
        /// README's "Signing keys and key rotation").
        /// </remarks>
        [JsonIgnore]
        public string? IdToken { get; }

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
        /// <param name="idToken">The validated ID token the identity was read from.</param>
        /// <returns>A successful result.</returns>
        internal static MicrosoftAuthCompletionResult Succeeded(MicrosoftUserInfo userInfo, string callerBinding, string idToken)
        {
            return new MicrosoftAuthCompletionResult(userInfo, callerBinding, idToken, null, FormatSubject(userInfo));
        }

        /// <summary>
        /// Creates a refusal of a genuine token, which names the account it was for.
        /// </summary>
        /// <param name="reason">Why the account was refused.</param>
        /// <param name="userInfo">The identity in the refused token.</param>
        /// <returns>A failed result.</returns>
        internal static MicrosoftAuthCompletionResult Refused(MicrosoftAuthFailureReason reason, MicrosoftUserInfo userInfo)
        {
            return new MicrosoftAuthCompletionResult(null, null, null, reason, FormatSubject(userInfo));
        }

        /// <summary>
        /// Creates a failure before any trustworthy account is known.
        /// </summary>
        /// <param name="reason">Why the flow failed.</param>
        /// <returns>A failed result.</returns>
        internal static MicrosoftAuthCompletionResult Failed(MicrosoftAuthFailureReason reason)
        {
            return new MicrosoftAuthCompletionResult(null, null, null, reason, null);
        }

        /// <summary>
        /// Describes the outcome without the caller binding or the identity's details, and with the ID token as
        /// <c>[REDACTED]</c>, so the result is safe to log. JSON serialization leaves out the same three.
        /// </summary>
        /// <returns>The outcome, the failure reason and the account as <c>tid:oid</c> when they are known.</returns>
        public override string ToString()
        {
            List<string> parts = new List<string> { $"{nameof(Success)} = {(Success ? "true" : "false")}" };

            if (FailureReason is MicrosoftAuthFailureReason reason)
            {
                parts.Add($"{nameof(FailureReason)} = {reason}");
            }

            if (AttemptedSubject != null)
            {
                parts.Add($"{nameof(AttemptedSubject)} = {AttemptedSubject}");
            }

            if (IdToken != null)
            {
                parts.Add($"{nameof(IdToken)} = [REDACTED]");
            }

            return $"{nameof(MicrosoftAuthCompletionResult)} {{ {string.Join(", ", parts)} }}";
        }

        private static string FormatSubject(MicrosoftUserInfo userInfo)
        {
            return $"{userInfo.TenantId:D}:{userInfo.ObjectId}";
        }
    }
}
