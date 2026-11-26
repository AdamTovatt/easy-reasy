using EasyReasy.Auth.OpenIdConnect;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The authentication context rule on the shared flow, both halves in one place: Begin asks for the context as the
    /// flow's only required claim, so on completion a required-claim refusal can only be the context's.
    /// </summary>
    internal static class MicrosoftAuthenticationContextRule
    {
        /// <summary>
        /// The flow's required claims for an authentication context: <c>acrs</c> holding it, and nothing else.
        /// </summary>
        /// <param name="authenticationContextId">The context the sign-in must satisfy, or null for none.</param>
        /// <returns>The required claims, or null when no context is required.</returns>
        public static IReadOnlyDictionary<string, string>? ToRequiredClaims(string? authenticationContextId)
        {
            return authenticationContextId == null
                ? null
                : new Dictionary<string, string> { [MicrosoftAuthority.AuthenticationContextClaim] = authenticationContextId };
        }

        /// <summary>
        /// Whether the flow's refusal is of the authentication context: a missing or mismatched required claim, since
        /// <see cref="ToRequiredClaims"/> asks for no other.
        /// </summary>
        /// <param name="reason">The reason the flow refused a genuine account.</param>
        /// <returns><c>true</c> when the refusal is the context's.</returns>
        public static bool IsRefusalOfTheContext(OpenIdConnectAuthFailureReason reason)
        {
            return reason is OpenIdConnectAuthFailureReason.MissingRequiredClaim
                or OpenIdConnectAuthFailureReason.RequiredClaimMismatch;
        }
    }
}
