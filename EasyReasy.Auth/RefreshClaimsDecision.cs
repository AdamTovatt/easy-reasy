using System.Security.Claims;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The verdict returned by <see cref="IRefreshClaimsResolver.ResolveAsync"/>.
    /// Construct via the static factory methods <see cref="Allow"/> and <see cref="Deny"/>.
    /// </summary>
    public sealed class RefreshClaimsDecision
    {
        /// <summary>
        /// Whether the resolver denied the refresh outright. When <c>true</c>,
        /// <see cref="Claims"/> and <see cref="Roles"/> are empty and
        /// <see cref="RefreshTokenService"/> returns
        /// <see cref="RefreshFailureReason.DeniedByResolver"/> without consuming the stored
        /// refresh token.
        /// </summary>
        public bool IsDenied { get; }

        /// <summary>
        /// The claims to place on the new access token and the new stored refresh token when
        /// the refresh is allowed. Empty when the refresh is denied.
        /// </summary>
        public IReadOnlyList<Claim> Claims { get; }

        /// <summary>
        /// The roles to place on the new access token and the new stored refresh token when
        /// the refresh is allowed. Empty when the refresh is denied.
        /// </summary>
        public IReadOnlyList<string> Roles { get; }

        private RefreshClaimsDecision(bool isDenied, IReadOnlyList<Claim> claims, IReadOnlyList<string> roles)
        {
            IsDenied = isDenied;
            Claims = claims;
            Roles = roles;
        }

        /// <summary>
        /// Creates a decision that allows the refresh to proceed with the supplied claims and roles,
        /// replacing what was originally stored at login time.
        /// </summary>
        /// <param name="claims">The claims to place on the new access token and the new stored refresh token.</param>
        /// <param name="roles">The roles to place on the new access token and the new stored refresh token.</param>
        /// <returns>An allow-decision carrying the supplied claims and roles.</returns>
        public static RefreshClaimsDecision Allow(IEnumerable<Claim> claims, IEnumerable<string> roles)
        {
            ArgumentNullException.ThrowIfNull(claims);
            ArgumentNullException.ThrowIfNull(roles);
            return new RefreshClaimsDecision(false, claims.ToList(), roles.ToList());
        }

        /// <summary>
        /// Creates a decision that denies the refresh. The stored refresh token is not consumed,
        /// so a subsequent legitimate refresh attempt is not affected.
        /// </summary>
        /// <returns>A deny-decision.</returns>
        public static RefreshClaimsDecision Deny()
        {
            return new RefreshClaimsDecision(true, Array.Empty<Claim>(), Array.Empty<string>());
        }
    }
}
