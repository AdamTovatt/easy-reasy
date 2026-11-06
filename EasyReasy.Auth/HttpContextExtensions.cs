using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Extension methods for accessing user and tenant id from the HTTP context.
    /// </summary>
    public static class HttpContextExtensions
    {
        /// <summary>
        /// Gets the tenant id from the HTTP context, if available.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>The tenant id, or null if not available.</returns>
        public static string? GetTenantId(this HttpContext context)
        {
            if (context == null)
                return null;
            // Prefer Items (set by ClaimsInjectionMiddleware), fallback to JWT claims
            if (context.Items.TryGetValue("TenantId", out object? value))
                return value as string;
            return context.GetClaimValue("tenant_id");
        }

        /// <summary>
        /// Gets the refresh token family id (claim type <c>"family_id"</c>) from the HTTP context, if available.
        /// This is the client's own opaque session identifier; it is intentionally readable from the signed
        /// access token, and possessing it grants no ability to revoke the session — retirement is a
        /// server-only operation via <see cref="IRefreshTokenService.RetireFamilyAsync"/>. A re-mint endpoint
        /// reads it to name the prior family it wants to retire.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>The refresh token family id, or null if not available.</returns>
        public static string? GetRefreshFamilyId(this HttpContext context) => context.GetClaimValue("family_id");

        /// <summary>
        /// Gets the user id from the HTTP context, if available.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>The user id, or null if not available.</returns>
        public static string? GetUserId(this HttpContext context)
        {
            if (context == null)
                return null;
            // Prefer Items (set by ClaimsInjectionMiddleware), fallback to JWT claims
            if (context.Items.TryGetValue("UserId", out object? value))
                return value as string;
            // Try both possible claim locations for user ID
            return context.GetClaimValue(ClaimTypes.NameIdentifier)
                ?? context.GetClaimValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        }

        /// <summary>
        /// Gets all roles for the current user from the HTTP context.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>An enumerable of role names, or an empty enumerable if none are present.</returns>
        public static IEnumerable<string> GetRoles(this HttpContext context)
        {
            if (context?.User?.Identity == null || !context.User.Identity.IsAuthenticated)
                return Enumerable.Empty<string>();
            return context.User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value);
        }

        /// <summary>
        /// Gets the value of a claim by its type/key from the HTTP context.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="claimType">The claim type/key to look for.</param>
        /// <returns>The claim value, or null if not found.</returns>
        public static string? GetClaimValue(this HttpContext context, string claimType)
        {
            if (context?.User?.Identity == null || !context.User.Identity.IsAuthenticated)
                return null;
            return context.User.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
        }

        /// <summary>
        /// Gets the value of a claim by its enum type from the HTTP context.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="claim">The claim enum to look for.</param>
        /// <returns>The claim value, or null if not found.</returns>
        public static string? GetClaimValue(this HttpContext context, EasyReasyClaim claim)
        {
            if (context == null)
                return null;
            switch (claim)
            {
                case EasyReasyClaim.UserId:
                    // Prefer Items, fallback to Claims
                    return context.Items.TryGetValue("UserId", out object? userId) ? userId as string : context.GetClaimValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
                case EasyReasyClaim.TenantId:
                    return context.Items.TryGetValue("TenantId", out object? tenantId) ? tenantId as string : context.GetClaimValue("tenant_id");
                case EasyReasyClaim.Email:
                    return context.GetClaimValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);
                case EasyReasyClaim.AuthType:
                    return context.GetClaimValue("auth_type");
                case EasyReasyClaim.RefreshFamilyId:
                    return context.GetClaimValue("family_id");
                case EasyReasyClaim.Issuer:
                    // JWT Issuer is not a claim, but a property on the token. Try to get it from ClaimsPrincipal if available.
                    ClaimsIdentity? jwt = context.User.Identity as ClaimsIdentity;
                    if (jwt != null && jwt.BootstrapContext is System.IdentityModel.Tokens.Jwt.JwtSecurityToken token)
                        return token.Issuer;
                    // Fallback: try to get from Items if set by custom middleware
                    return context.Items.TryGetValue("Issuer", out object? issuer) ? issuer as string : null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The <see cref="HttpContext.Items"/> key under which an endpoint records that it authenticated the caller.
        /// </summary>
        private const string AuthenticationSucceededKey = "EasyReasy.Auth.AuthenticationSucceeded";

        /// <summary>
        /// Sets <c>Cache-Control: no-store</c> on the response, so neither a browser nor a proxy keeps a copy of it.
        /// Every endpoint that answers with a token, a session or a sign-in URL sets it, the built-in ones included.
        /// </summary>
        /// <param name="context">The HTTP context of the current request.</param>
        public static void SetNoStoreCacheControl(this HttpContext context)
        {
            context.Response.Headers.CacheControl = "no-store";
        }

        /// <summary>
        /// Records that this request authenticated the caller and issued a token.
        /// </summary>
        /// <remarks>
        /// Call this from any endpoint that issues a token, including one outside this package.
        /// <see cref="ProgressiveDelayMiddleware"/> clears an IP's accumulated failure count only for a request
        /// marked this way, so an endpoint that cannot authenticate anyone — the logout endpoint, a 404, a
        /// credential-less request — cannot be used to reset the delay between guesses.
        /// </remarks>
        /// <param name="context">The HTTP context of the request that authenticated the caller.</param>
        public static void MarkAuthenticationSucceeded(this HttpContext context)
        {
            context.Items[AuthenticationSucceededKey] = true;
        }

        /// <summary>
        /// Whether the access-token cookie is on: <see cref="EasyReasyAuthOptions.CookieTokenPaths"/> lists at least
        /// one prefix. Ask this before calling <see cref="SetAccessTokenCookie"/>, <see cref="TrySetAccessTokenCookie"/>
        /// or <see cref="ClearAccessTokenCookie"/> from code that runs whether or not the option is on, since all three
        /// throw when it is off. The built-in endpoints ask it the same way.
        /// </summary>
        /// <param name="context">The HTTP context of the current request.</param>
        /// <returns>
        /// <c>true</c> when <c>AddEasyReasyAuth</c> was called with a non-empty <see cref="EasyReasyAuthOptions.CookieTokenPaths"/>.
        /// </returns>
        public static bool IsAccessTokenCookieEnabled(this HttpContext context)
        {
            return FindEnabledAccessTokenCookie(context) != null;
        }

        /// <summary>
        /// Writes the access-token cookie, one per prefix in <see cref="EasyReasyAuthOptions.CookieTokenPaths"/>, so a
        /// plain navigation from this browser to one of those paths, such as a file opened in a new tab, is
        /// authenticated by <paramref name="accessToken"/>.
        /// </summary>
        /// <remarks>
        /// Call it from an endpoint of your own wherever it hands a browser an access token, before anything that
        /// cannot be undone; where the session is already committed, such as after a refresh, call
        /// <see cref="TrySetAccessTokenCookie"/> instead. The cookie is named
        /// <c>__Secure-access_token</c> and is <c>HttpOnly</c>, <c>Secure</c>, <c>SameSite=Strict</c> and host-only,
        /// with <c>Path</c> set to <c>PathBase</c> plus the prefix and <c>Max-Age</c> set to the token's own
        /// <c>exp</c> minus now on the registered <see cref="TimeProvider"/>. The token is read, not validated.
        /// </remarks>
        /// <param name="context">The HTTP context of the response to set the cookie on.</param>
        /// <param name="accessToken">The access token the response issues.</param>
        /// <exception cref="ArgumentException">Thrown when the token is not a JWT or has no <c>exp</c> claim.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="EasyReasyAuthOptions.CookieTokenPaths"/> is empty or <c>AddEasyReasyAuth</c> was not
        /// called, or when the cookie's name and value together would exceed 4096 bytes, which browsers drop silently.
        /// </exception>
        public static void SetAccessTokenCookie(this HttpContext context, string accessToken)
        {
            AccessTokenCookie cookie = GetEnabledAccessTokenCookie(context, nameof(SetAccessTokenCookie));
            TimeProvider timeProvider = context.RequestServices.GetRequiredService<TimeProvider>();
            cookie.WriteCookies(context, accessToken, timeProvider);
        }

        /// <summary>
        /// Writes the access-token cookie as <see cref="SetAccessTokenCookie"/> does, except that a token too large
        /// for a cookie (name and value over 4096 bytes) expires the cookie instead, logs an error naming the size and
        /// the limit, and returns <c>false</c>, so the response and the session it carries survive.
        /// </summary>
        /// <remarks>
        /// For an endpoint that has already committed the session when it writes the cookie. A refresh has used up
        /// the old refresh token by then, so failing the response would lose the new one, and claims re-evaluated on
        /// refresh can grow a token past the limit after a login that fit. The built-in login, Google sign-in and
        /// refresh endpoints call this when the option is on. The error is logged through the logging registered in
        /// <see cref="HttpContext.RequestServices"/>, under the category <c>EasyReasy.Auth.AccessTokenCookie</c>.
        /// </remarks>
        /// <param name="context">The HTTP context of the response to set the cookie on.</param>
        /// <param name="accessToken">The access token the response issues.</param>
        /// <returns><c>true</c> when the cookie was written; <c>false</c> when the token was too large and the cookie was expired instead.</returns>
        /// <exception cref="ArgumentException">Thrown when the token is not a JWT or has no <c>exp</c> claim.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="EasyReasyAuthOptions.CookieTokenPaths"/> is empty or <c>AddEasyReasyAuth</c> was not called.
        /// </exception>
        public static bool TrySetAccessTokenCookie(this HttpContext context, string accessToken)
        {
            AccessTokenCookie cookie = GetEnabledAccessTokenCookie(context, nameof(TrySetAccessTokenCookie));
            TimeProvider timeProvider = context.RequestServices.GetRequiredService<TimeProvider>();

            // ILogger<AccessTokenCookie> rather than a hand-written category string, so the compiler tracks the
            // category the README tells consumers to watch.
            ILogger<AccessTokenCookie> logger = context.RequestServices.GetService<ILogger<AccessTokenCookie>>()
                ?? NullLogger<AccessTokenCookie>.Instance;
            return cookie.TryWriteCookies(context, accessToken, timeProvider, logger);
        }

        /// <summary>
        /// Expires the access-token cookie on every prefix in <see cref="EasyReasyAuthOptions.CookieTokenPaths"/>.
        /// </summary>
        /// <remarks>
        /// The built-in refresh endpoint calls this when a refresh fails, and the built-in logout endpoint on every
        /// response, when the option is on. Call it from an endpoint of your own wherever it ends or refuses a
        /// session: every failed or refused refresh, and your own logout. The token the cookie carried stays valid
        /// until its <c>exp</c>, as a header token does.
        /// </remarks>
        /// <param name="context">The HTTP context of the response to clear the cookie on.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="EasyReasyAuthOptions.CookieTokenPaths"/> is empty or <c>AddEasyReasyAuth</c> was not called.
        /// </exception>
        public static void ClearAccessTokenCookie(this HttpContext context)
        {
            GetEnabledAccessTokenCookie(context, nameof(ClearAccessTokenCookie)).ClearCookies(context);
        }

        /// <summary>
        /// Returns the registered access-token cookie, throwing rather than letting a helper silently do nothing when
        /// the option is off.
        /// </summary>
        private static AccessTokenCookie GetEnabledAccessTokenCookie(HttpContext context, string helperName)
        {
            return FindEnabledAccessTokenCookie(context)
                ?? throw new InvalidOperationException(
                    $"{helperName} needs {nameof(EasyReasyAuthOptions)}.{nameof(EasyReasyAuthOptions.CookieTokenPaths)} to list at least one prefix in {nameof(AuthServiceCollectionExtensions.AddEasyReasyAuth)}, " +
                    $"since without one no cookie would be written or read. Check {nameof(IsAccessTokenCookieEnabled)} first in code that runs either way.");
        }

        /// <summary>
        /// Returns the access-token cookie <c>AddEasyReasyAuth</c> registered when it lists at least one prefix, or
        /// <c>null</c> when the option is off or <c>AddEasyReasyAuth</c> was not called.
        /// </summary>
        private static AccessTokenCookie? FindEnabledAccessTokenCookie(HttpContext context)
        {
            AccessTokenCookie? cookie = context.RequestServices?.GetService<AccessTokenCookie>();
            return cookie?.IsEnabled == true ? cookie : null;
        }

        /// <summary>
        /// Whether this request authenticated the caller, as recorded by
        /// <see cref="MarkAuthenticationSucceeded"/>.
        /// </summary>
        /// <param name="context">The HTTP context to inspect.</param>
        /// <returns><c>true</c> when an endpoint marked this request as having authenticated the caller.</returns>
        public static bool HasAuthenticationSucceeded(this HttpContext context)
        {
            return context.Items.TryGetValue(AuthenticationSucceededKey, out object? succeeded) && succeeded is true;
        }

        /// <summary>
        /// The <see cref="HttpContext.Items"/> key under which <see cref="RefuseAuthenticatedCaller"/> records that
        /// the request's 401 refuses a caller whose token validated.
        /// </summary>
        private const string AuthenticatedCallerRefusedKey = "EasyReasy.Auth.AuthenticatedCallerRefused";

        /// <summary>
        /// Answers 401 Unauthorized to a caller whose token validated but whom the app refuses because of the
        /// account's state, such as a deactivated account.
        /// </summary>
        /// <remarks>
        /// Sets the response status to 401 and records, in the same step, that the 401 is a refusal rather than a
        /// failed credential. <see cref="ProgressiveDelayMiddleware"/> neither counts such a 401 towards the IP's
        /// delay nor clears the count it has, provided the request carries an authenticated principal; a refusal
        /// of a request without one still counts, so a mistaken call cannot exempt an anonymous password guess.
        /// The delay applied before the request runs is unchanged.
        /// <para>
        /// Never use it to reject a failed credential from a signed-in caller, such as a wrong current password
        /// or a wrong TOTP or WebAuthn step-up code: those are guesses, and this call would exempt them from
        /// the delay. Answer them with a plain 401.
        /// </para>
        /// </remarks>
        /// <param name="context">The HTTP context of the request to refuse.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown by the server when the response has already started, since its status can no longer change.
        /// </exception>
        public static void RefuseAuthenticatedCaller(this HttpContext context)
        {
            // Status first: if the response has already started this throws, and no marker is left behind.
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Items[AuthenticatedCallerRefusedKey] = true;
        }

        /// <summary>
        /// Whether this request refused a caller whose token validated: it was refused through
        /// <see cref="RefuseAuthenticatedCaller"/> and carries an authenticated principal.
        /// </summary>
        /// <param name="context">The HTTP context to inspect.</param>
        /// <returns>
        /// <c>true</c> when the request was refused through <see cref="RefuseAuthenticatedCaller"/> and its caller
        /// is authenticated.
        /// </returns>
        internal static bool WasAuthenticatedCallerRefused(this HttpContext context)
        {
            // The marker alone is not enough: one set by mistake on an anonymous or bad-token request would
            // otherwise let a password guess escape the progressive delay.
            return context.Items.TryGetValue(AuthenticatedCallerRefusedKey, out object? refused)
                && refused is true
                && context.User.Identity?.IsAuthenticated == true;
        }
    }
}
