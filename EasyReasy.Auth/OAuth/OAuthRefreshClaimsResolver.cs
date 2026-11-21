using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Puts <see cref="IOAuthGrantHandler.RefreshAsync"/> in the resolver's place inside <see cref="RefreshTokenService"/>
    /// for a client's refresh, and keeps the server's claims (<see cref="OAuthServerClaims"/>) its own: stripped from what
    /// the handler sees and returns, and the client id set from the client the refresh token is bound to.
    /// </summary>
    internal sealed class OAuthRefreshClaimsResolver : IRefreshClaimsResolver
    {
        private readonly IOAuthGrantHandler _handler;
        private readonly string _grantId;
        private readonly string _clientId;
        private readonly HttpContext _httpContext;

        /// <summary>
        /// Initializes the resolver.
        /// </summary>
        /// <param name="handler">The application's handler.</param>
        /// <param name="grantId">The grant of the refresh token being used.</param>
        /// <param name="clientId">The client the refresh token is bound to.</param>
        /// <param name="httpContext">The token request.</param>
        public OAuthRefreshClaimsResolver(IOAuthGrantHandler handler, string grantId, string clientId, HttpContext httpContext)
        {
            _handler = handler;
            _grantId = grantId;
            _clientId = clientId;
            _httpContext = httpContext;
        }

        /// <inheritdoc />
        public async Task<RefreshClaimsDecision> ResolveAsync(RefreshClaimsContext context, CancellationToken cancellationToken)
        {
            OAuthRefreshContext refreshContext = new OAuthRefreshContext
            {
                GrantId = _grantId,
                ClientId = _clientId,
                Subject = context.Subject,
                AuthType = context.AuthType,
                StoredClaims = OAuthServerClaims.Without(context.StoredClaims),
                StoredRoles = context.StoredRoles,
                HttpContext = _httpContext,
            };

            RefreshClaimsDecision decision = await _handler.RefreshAsync(refreshContext, cancellationToken);
            if (decision.IsDenied)
            {
                return decision;
            }

            return RefreshClaimsDecision.Allow(OAuthServerClaims.WithClientId(decision.Claims, _clientId), decision.Roles);
        }
    }
}
