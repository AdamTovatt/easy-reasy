using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The outcome of <see cref="OpenIdConnectCodeFlow.CompleteAsync"/>, for a provider package to apply its own rules
    /// to: the failure reason, and the validated identity whenever the ID token itself validated, including when one of
    /// the flow's rules then refused it. An application uses <see cref="IOpenIdConnectAuthService"/> and its
    /// <see cref="OpenIdConnectAuthCompletionResult"/> instead.
    /// </summary>
    public sealed class OpenIdConnectCodeFlowResult
    {
        private OpenIdConnectCodeFlowResult(
            OpenIdConnectAuthFailureReason? failureReason,
            OpenIdConnectUserInfo? userInfo,
            string? callerBinding,
            string? idToken,
            string? idTokenSigningKey)
        {
            FailureReason = failureReason;
            UserInfo = userInfo;
            CallerBinding = callerBinding;
            IdToken = idToken;
            IdTokenSigningKey = idTokenSigningKey;
        }

        /// <summary>
        /// Whether every check passed.
        /// </summary>
        [MemberNotNullWhen(true, nameof(UserInfo), nameof(CallerBinding), nameof(IdToken), nameof(IdTokenSigningKey))]
        [MemberNotNullWhen(false, nameof(FailureReason))]
        public bool Success => FailureReason == null;

        /// <summary>
        /// Why the flow was refused. Only populated when <see cref="Success"/> is false.
        /// </summary>
        public OpenIdConnectAuthFailureReason? FailureReason { get; }

        /// <summary>
        /// The identity in the ID token, populated whenever the token validated: on success, and on a refusal by one of
        /// the flow's rules (identity, sign-in time, <c>acr</c>, <c>amr</c>), so a provider package can apply its own
        /// rules first and name the account in its refusal. Null when the state, the cookie, the response, the provider,
        /// the code or the token failed. Never act on it unless <see cref="Success"/> is true.
        /// </summary>
        [JsonIgnore]
        public OpenIdConnectUserInfo? UserInfo { get; }

        /// <summary>
        /// The caller binding the flow was begun with. Only populated when <see cref="Success"/> is true.
        /// </summary>
        [JsonIgnore]
        public string? CallerBinding { get; }

        /// <summary>
        /// The raw ID token <see cref="UserInfo"/> was read from. Only populated when <see cref="Success"/> is true. A
        /// bearer credential: see <see cref="OpenIdConnectAuthCompletionResult.IdToken"/>.
        /// </summary>
        [JsonIgnore]
        public string? IdToken { get; }

        /// <summary>
        /// The provider's public key <see cref="IdToken"/> was verified with, as JWK JSON. Only populated when
        /// <see cref="Success"/> is true. See <see cref="OpenIdConnectAuthCompletionResult.IdTokenSigningKey"/>.
        /// </summary>
        [JsonIgnore]
        public string? IdTokenSigningKey { get; }

        internal static OpenIdConnectCodeFlowResult Succeeded(OpenIdConnectUserInfo userInfo, string callerBinding, string idToken, string idTokenSigningKey)
        {
            return new OpenIdConnectCodeFlowResult(null, userInfo, callerBinding, idToken, idTokenSigningKey);
        }

        internal static OpenIdConnectCodeFlowResult Refused(OpenIdConnectAuthFailureReason reason, OpenIdConnectUserInfo userInfo)
        {
            return new OpenIdConnectCodeFlowResult(reason, userInfo, null, null, null);
        }

        internal static OpenIdConnectCodeFlowResult Failed(OpenIdConnectAuthFailureReason reason)
        {
            return new OpenIdConnectCodeFlowResult(reason, null, null, null, null);
        }

        /// <summary>
        /// Describes the outcome without the identity, the caller binding or the ID token, so the result is safe to log.
        /// </summary>
        /// <returns>The outcome and the failure reason.</returns>
        public override string ToString()
        {
            return Success
                ? $"{nameof(OpenIdConnectCodeFlowResult)} {{ {nameof(Success)} = true }}"
                : $"{nameof(OpenIdConnectCodeFlowResult)} {{ {nameof(Success)} = false, {nameof(FailureReason)} = {FailureReason} }}";
        }
    }
}
