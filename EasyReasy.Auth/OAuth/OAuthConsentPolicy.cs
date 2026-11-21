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
    /// An API key's token is refused by its auth type, since a key acts for no person who could consent. The consent
    /// endpoints never opt in to connections, so the bearer's audience check rejects a client's token before
    /// this policy runs; the policy refuses it as well, so neither alone keeps a connection out.
    /// </remarks>
    internal static class OAuthConsentPolicy
    {
        /// <summary>
        /// Builds the policy: authenticated, auth type <see cref="EasyReasyAuthTypes.User"/>, and no client id claim.
        /// </summary>
        /// <returns>The policy.</returns>
        public static AuthorizationPolicy Build()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(EasyReasyClaimTypes.AuthType, EasyReasyAuthTypes.User)
                .RequireAssertion(context => !OAuthServerClaims.IsConnection(context.User))
                .Build();
        }
    }
}
