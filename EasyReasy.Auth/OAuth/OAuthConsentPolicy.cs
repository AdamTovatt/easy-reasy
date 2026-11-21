using Microsoft.AspNetCore.Authorization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Who may read, approve and deny a pending authorization request: a person signed in to the application itself.
    /// </summary>
    /// <remarks>
    /// A client's own access token is refused, whatever auth type its grant carries, by its
    /// <see cref="EasyReasyClaimTypes.ClientId"/> claim: otherwise a connected client could send itself through
    /// <c>/oauth/authorize</c>, approve with its own token and widen its access, or mint a new grant ahead of a revocation.
    /// An API key's token is refused by its auth type, since a key acts for no person who could consent.
    /// </remarks>
    internal static class OAuthConsentPolicy
    {
        /// <summary>
        /// The auth type of the application's first-party session tokens (as in EasyReasy.Auth's <c>UserOnly</c> policy).
        /// </summary>
        public const string PersonAuthType = "user";

        /// <summary>
        /// Builds the policy: authenticated, auth type <see cref="PersonAuthType"/>, and no client id claim.
        /// </summary>
        /// <returns>The policy.</returns>
        public static AuthorizationPolicy Build()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(EasyReasyClaimTypes.AuthType, PersonAuthType)
                .RequireAssertion(context => !context.User.HasClaim(claim => claim.Type == EasyReasyClaimTypes.ClientId))
                .Build();
        }
    }
}
