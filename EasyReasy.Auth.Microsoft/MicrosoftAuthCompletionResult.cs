using EasyReasy.Auth.OpenIdConnect;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The outcome of <see cref="IMicrosoftAuthService.CompleteAsync"/>: the validated identity and the ID token it was
    /// read from, or the reason the flow was refused: the shared flow's, or the Entra rules'.
    /// </summary>
    /// <remarks>
    /// Built only through its factories, each of which sets at most one reason, so a result can never carry both
    /// reasons, and a failure always carries one.
    /// </remarks>
    public sealed class MicrosoftAuthCompletionResult
    {
        private MicrosoftAuthCompletionResult(
            MicrosoftUserInfo? userInfo,
            string? idToken,
            string? idTokenSigningKey,
            OpenIdConnectAuthFailureReason? flowFailureReason,
            MicrosoftAuthFailureReason? microsoftFailureReason,
            string? attemptedSubject)
        {
            UserInfo = userInfo;
            IdToken = idToken;
            IdTokenSigningKey = idTokenSigningKey;
            FlowFailureReason = flowFailureReason;
            MicrosoftFailureReason = microsoftFailureReason;
            AttemptedSubject = attemptedSubject;
        }

        /// <summary>
        /// Whether every check passed.
        /// </summary>
        [MemberNotNullWhen(true, nameof(UserInfo), nameof(IdToken), nameof(IdTokenSigningKey), nameof(AttemptedSubject))]
        [MemberNotNullWhen(false, nameof(FailureDetail), nameof(FailureCategory))]
        public bool Success => FlowFailureReason == null && MicrosoftFailureReason == null;

        /// <summary>
        /// The validated identity. Only populated when <see cref="Success"/> is true. Personal data, so it is left out
        /// of <see cref="ToString"/> and of JSON serialization.
        /// </summary>
        [JsonIgnore]
        public MicrosoftUserInfo? UserInfo { get; }

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
        /// <see cref="ExternalAuthResult"/> carries it. To check it later, keep <see cref="IdTokenSigningKey"/> along
        /// with it, since Microsoft rotates and retires its keys.
        /// </remarks>
        [JsonIgnore]
        public string? IdToken { get; }

        /// <summary>
        /// Microsoft's public key <see cref="IdToken"/> was verified with, as JWK JSON. Only populated when
        /// <see cref="Success"/> is true. Public, so not a secret; keep it with the ID token to verify its signature after
        /// Microsoft has retired the key.
        /// </summary>
        /// <remarks>
        /// The key as this server verified with it, rewritten as JWK JSON with Microsoft's <c>x5c</c> certificate. A later
        /// check shows the token was signed by this key; that the key was Microsoft's rests on this application's record
        /// that it read it from Microsoft's key set at sign-in, so seal the key with the token and the record it vouches
        /// for.
        /// </remarks>
        [JsonIgnore]
        public string? IdTokenSigningKey { get; }

        /// <summary>
        /// Why the shared flow refused the sign-in: its state, binding cookie, response, code or token, or its max age
        /// rule. A token whose <c>tid</c> or <c>oid</c> this package cannot read is reported here too, as
        /// <see cref="OpenIdConnectAuthFailureReason.InvalidToken"/>, the name its audit records have always carried.
        /// Null on success and when <see cref="MicrosoftFailureReason"/> is set.
        /// </summary>
        public OpenIdConnectAuthFailureReason? FlowFailureReason { get; }

        /// <summary>
        /// Why the Entra rules refused the account: its tenant or its authentication context. Null on success and when
        /// <see cref="FlowFailureReason"/> is set.
        /// </summary>
        public MicrosoftAuthFailureReason? MicrosoftFailureReason { get; }

        /// <summary>
        /// The name of whichever reason is set, as the sign-in endpoint records it in
        /// <see cref="ExternalAuthResult.FailureDetail"/>. Null on success.
        /// </summary>
        [JsonIgnore]
        public string? FailureDetail => FlowFailureReason?.ToString() ?? MicrosoftFailureReason?.ToString();

        /// <summary>
        /// The category the sign-in endpoint files the refusal under in <see cref="ExternalAuthResult.FailureReason"/>:
        /// a flow reason's <see cref="OpenIdConnectAuthFailureReasonExtensions.ToExternalAuthFailureReason"/>, and
        /// <see cref="ExternalAuthFailureReason.Rejected"/> for an Entra refusal, always of a genuine account. Null on
        /// success.
        /// </summary>
        [JsonIgnore]
        public ExternalAuthFailureReason? FailureCategory => FlowFailureReason is OpenIdConnectAuthFailureReason flowReason
            ? flowReason.ToExternalAuthFailureReason()
            : MicrosoftFailureReason != null ? ExternalAuthFailureReason.Rejected : null;

        /// <summary>
        /// The account as <c>tid:oid</c>, for an audit record: populated on success, and on a refusal of a genuine
        /// token (its tenant, authentication context or sign-in time). Null when the token itself, the state, the
        /// cookie, the caller binding, the code or the account's <c>tid</c> or <c>oid</c> failed, because no trustworthy
        /// account is known then.
        /// </summary>
        public string? AttemptedSubject { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="userInfo">The validated identity.</param>
        /// <param name="idToken">The validated ID token the identity was read from.</param>
        /// <param name="idTokenSigningKey">The public key the ID token was verified with, as JWK JSON.</param>
        /// <returns>A successful result.</returns>
        internal static MicrosoftAuthCompletionResult Succeeded(MicrosoftUserInfo userInfo, string idToken, string idTokenSigningKey)
        {
            return new MicrosoftAuthCompletionResult(userInfo, idToken, idTokenSigningKey, null, null, FormatSubject(userInfo));
        }

        /// <summary>
        /// Creates a refusal of a genuine token by the Entra rules, which names the account it was for.
        /// </summary>
        /// <param name="reason">Why the account was refused.</param>
        /// <param name="userInfo">The identity in the refused token.</param>
        /// <returns>A failed result.</returns>
        internal static MicrosoftAuthCompletionResult Refused(MicrosoftAuthFailureReason reason, MicrosoftUserInfo userInfo)
        {
            return new MicrosoftAuthCompletionResult(null, null, null, null, reason, FormatSubject(userInfo));
        }

        /// <summary>
        /// Creates a refusal of a genuine token by the shared flow's rules, which names the account it was for.
        /// </summary>
        /// <param name="reason">Why the account was refused.</param>
        /// <param name="userInfo">The identity in the refused token.</param>
        /// <returns>A failed result.</returns>
        internal static MicrosoftAuthCompletionResult Refused(OpenIdConnectAuthFailureReason reason, MicrosoftUserInfo userInfo)
        {
            return new MicrosoftAuthCompletionResult(null, null, null, reason, null, FormatSubject(userInfo));
        }

        /// <summary>
        /// Creates a failure before any trustworthy account is known: the shared flow's, or this package's unreadable
        /// <c>tid</c> or <c>oid</c> as <see cref="OpenIdConnectAuthFailureReason.InvalidToken"/>.
        /// </summary>
        /// <param name="reason">Why the flow failed.</param>
        /// <returns>A failed result.</returns>
        internal static MicrosoftAuthCompletionResult Failed(OpenIdConnectAuthFailureReason reason)
        {
            return new MicrosoftAuthCompletionResult(null, null, null, reason, null, null);
        }

        /// <summary>
        /// Describes the outcome without the identity's details, and with the ID token as <c>[REDACTED]</c>, so the result
        /// is safe to log. JSON serialization leaves out the same, and the signing key.
        /// </summary>
        /// <returns>The outcome, the failure reason and the account as <c>tid:oid</c> when they are known.</returns>
        public override string ToString()
        {
            List<string> parts = new List<string> { $"{nameof(Success)} = {(Success ? "true" : "false")}" };

            if (FlowFailureReason is OpenIdConnectAuthFailureReason flowReason)
            {
                parts.Add($"{nameof(FlowFailureReason)} = {flowReason}");
            }

            if (MicrosoftFailureReason is MicrosoftAuthFailureReason microsoftReason)
            {
                parts.Add($"{nameof(MicrosoftFailureReason)} = {microsoftReason}");
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
