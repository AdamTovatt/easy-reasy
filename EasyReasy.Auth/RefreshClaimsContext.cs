using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The context supplied to <see cref="IRefreshClaimsResolver.ResolveAsync"/> on every refresh.
    /// </summary>
    public sealed class RefreshClaimsContext
    {
        /// <summary>
        /// The subject identifier (user id) the refresh token was originally issued to.
        /// </summary>
        public string Subject { get; }

        /// <summary>
        /// The authentication type the refresh token was originally issued under, e.g. "user" or "apikey".
        /// </summary>
        public string AuthType { get; }

        /// <summary>
        /// The claims persisted on the refresh token row that just validated. Initially the
        /// claims serialized at login time; if a resolver has previously returned
        /// <see cref="RefreshClaimsDecision.Allow"/>, the claims that resolver supplied on the
        /// most recent refresh in this family.
        /// </summary>
        public IReadOnlyList<Claim> StoredClaims { get; }

        /// <summary>
        /// The roles persisted on the refresh token row that just validated. Initially the
        /// roles serialized at login time; if a resolver has previously returned
        /// <see cref="RefreshClaimsDecision.Allow"/>, the roles that resolver supplied on the
        /// most recent refresh in this family.
        /// </summary>
        public IReadOnlyList<string> StoredRoles { get; }

        /// <summary>
        /// The HTTP context for the refresh request, or <c>null</c> when
        /// <see cref="IRefreshTokenService.RefreshAsync"/> is invoked outside an HTTP request
        /// (for example from a background job). Resolvers must tolerate <c>null</c>.
        /// </summary>
        public HttpContext? HttpContext { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshClaimsContext"/> class.
        /// </summary>
        /// <param name="subject">The subject identifier the refresh token was issued to.</param>
        /// <param name="authType">The authentication type the refresh token was issued under.</param>
        /// <param name="storedClaims">The claims that were originally serialized at login time.</param>
        /// <param name="storedRoles">The roles that were originally serialized at login time.</param>
        /// <param name="httpContext">The HTTP context for the refresh request, or <c>null</c> for non-HTTP callers.</param>
        public RefreshClaimsContext(
            string subject,
            string authType,
            IReadOnlyList<Claim> storedClaims,
            IReadOnlyList<string> storedRoles,
            HttpContext? httpContext)
        {
            ArgumentNullException.ThrowIfNull(subject);
            ArgumentNullException.ThrowIfNull(authType);
            ArgumentNullException.ThrowIfNull(storedClaims);
            ArgumentNullException.ThrowIfNull(storedRoles);

            Subject = subject;
            AuthType = authType;
            StoredClaims = storedClaims;
            StoredRoles = storedRoles;
            HttpContext = httpContext;
        }
    }
}
