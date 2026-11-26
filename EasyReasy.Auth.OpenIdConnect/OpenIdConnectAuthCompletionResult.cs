using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The outcome of <see cref="IOpenIdConnectAuthService.CompleteAsync"/>: the validated identity, the caller binding
    /// from Begin and the ID token it was read from, or the reason the flow was refused.
    /// </summary>
    /// <remarks>
    /// An application that keeps its own audit of each sign-in writes it from this result: the caller binding, the
    /// account and, in <see cref="UserInfo"/>, <c>auth_time</c>, <c>acr</c> and <c>amr</c>, with any required claims in
    /// its <see cref="OpenIdConnectUserInfo.Claims"/>.
    /// </remarks>
    public sealed class OpenIdConnectAuthCompletionResult
    {
        private OpenIdConnectAuthCompletionResult(
            OpenIdConnectUserInfo? userInfo,
            string? callerBinding,
            string? idToken,
            string? idTokenSigningKey,
            OpenIdConnectAuthFailureReason? failureReason,
            OpenIdConnectIdentity? attemptedIdentity)
        {
            UserInfo = userInfo;
            CallerBinding = callerBinding;
            IdToken = idToken;
            IdTokenSigningKey = idTokenSigningKey;
            FailureReason = failureReason;
            AttemptedIdentity = attemptedIdentity;
        }

        /// <summary>
        /// Whether every check passed.
        /// </summary>
        [MemberNotNullWhen(true, nameof(UserInfo), nameof(CallerBinding), nameof(IdToken), nameof(IdTokenSigningKey), nameof(AttemptedIdentity))]
        [MemberNotNullWhen(false, nameof(FailureReason))]
        public bool Success => FailureReason == null;

        /// <summary>
        /// The validated identity. Only populated when <see cref="Success"/> is true. Personal data, so it is left out of
        /// <see cref="ToString"/> and of JSON serialization.
        /// </summary>
        [JsonIgnore]
        public OpenIdConnectUserInfo? UserInfo { get; }

        /// <summary>
        /// The <see cref="OpenIdConnectAuthBeginRequest.CallerBinding"/> the flow was begun with. Only populated when
        /// <see cref="Success"/> is true. Compare it with what the completing endpoint expects before acting on the
        /// identity. The application's own value, which may name a user or a record, so it is left out of
        /// <see cref="ToString"/> and of JSON serialization.
        /// </summary>
        [JsonIgnore]
        public string? CallerBinding { get; }

        /// <summary>
        /// The raw ID token <see cref="UserInfo"/> was read from, exactly as the provider's token endpoint returned it and
        /// after every check passed. Only populated when <see cref="Success"/> is true.
        /// </summary>
        /// <remarks>
        /// Evidence for a record signed through a re-check: the provider's own signature over the account, the sign-in
        /// time, <c>acr</c>, <c>amr</c> and the nonce. Seal it with the record it vouches for (encrypted, with access to
        /// it audited); never log it or send it to a browser. It is a bearer credential for this client id until it
        /// expires. <see cref="ToString"/> shows it as <c>[REDACTED]</c>, JSON serialization leaves it out, and the
        /// sign-in endpoints never pass it on. To check it later, seal <see cref="IdTokenSigningKey"/> with it, since
        /// providers rotate and retire keys.
        /// </remarks>
        [JsonIgnore]
        public string? IdToken { get; }

        /// <summary>
        /// The provider's public key <see cref="IdToken"/>'s signature verified with, as a JSON Web Key (its <c>kty</c>,
        /// <c>kid</c> and public members, never a private one). Only populated when <see cref="Success"/> is true.
        /// </summary>
        /// <remarks>
        /// Keep it with the sealed ID token: the provider drops a key from its key set once it rotates it out, and this is
        /// what lets the token's signature be checked after that. Public, so not a secret; left out of JSON serialization
        /// only because it belongs with the token.
        /// </remarks>
        [JsonIgnore]
        public string? IdTokenSigningKey { get; }

        /// <summary>
        /// Why the flow was refused. Only populated when <see cref="Success"/> is false.
        /// </summary>
        public OpenIdConnectAuthFailureReason? FailureReason { get; }

        /// <summary>
        /// The account, for an audit record: populated on success, and on a refusal of a genuine token (its identity,
        /// sign-in time, <c>acr</c> or <c>amr</c>). Null when the state, the cookie, the response, the provider, the code or
        /// the token failed, because no trustworthy account is known then.
        /// </summary>
        public OpenIdConnectIdentity? AttemptedIdentity { get; }

        internal static OpenIdConnectAuthCompletionResult Succeeded(OpenIdConnectUserInfo userInfo, string callerBinding, string idToken, string idTokenSigningKey)
        {
            return new OpenIdConnectAuthCompletionResult(userInfo, callerBinding, idToken, idTokenSigningKey, null, userInfo.Identity);
        }

        internal static OpenIdConnectAuthCompletionResult Refused(OpenIdConnectAuthFailureReason reason, OpenIdConnectIdentity attemptedIdentity)
        {
            return new OpenIdConnectAuthCompletionResult(null, null, null, null, reason, attemptedIdentity);
        }

        internal static OpenIdConnectAuthCompletionResult Failed(OpenIdConnectAuthFailureReason reason)
        {
            return new OpenIdConnectAuthCompletionResult(null, null, null, null, reason, null);
        }

        /// <summary>
        /// The application-facing form of the shared flow's outcome: the identity only on success, the account alone on a
        /// refusal of a genuine token.
        /// </summary>
        internal static OpenIdConnectAuthCompletionResult From(OpenIdConnectCodeFlowResult result)
        {
            if (result.Success)
            {
                return Succeeded(result.UserInfo, result.CallerBinding, result.IdToken, result.IdTokenSigningKey);
            }

            return result.UserInfo != null
                ? Refused(result.FailureReason.Value, result.UserInfo.Identity)
                : Failed(result.FailureReason.Value);
        }

        /// <summary>
        /// Describes the outcome without the caller binding or the identity's details, and with the ID token as
        /// <c>[REDACTED]</c>, so the result is safe to log. JSON serialization leaves out the same three.
        /// </summary>
        /// <returns>The outcome, the failure reason and the account when they are known.</returns>
        public override string ToString()
        {
            List<string> parts = new List<string> { $"{nameof(Success)} = {(Success ? "true" : "false")}" };

            if (FailureReason is OpenIdConnectAuthFailureReason reason)
            {
                parts.Add($"{nameof(FailureReason)} = {reason}");
            }

            if (AttemptedIdentity is OpenIdConnectIdentity identity)
            {
                parts.Add($"{nameof(AttemptedIdentity)} = {identity}");
            }

            if (IdToken != null)
            {
                parts.Add($"{nameof(IdToken)} = [REDACTED]");
            }

            return $"{nameof(OpenIdConnectAuthCompletionResult)} {{ {string.Join(", ", parts)} }}";
        }
    }
}
