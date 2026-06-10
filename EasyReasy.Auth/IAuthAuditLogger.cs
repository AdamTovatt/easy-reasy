using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Optional service that receives structured notifications for every authentication event
    /// the library surfaces. Register an implementation in DI to emit ISO 27001 A.12.4.1 audit
    /// records (successful authentications, failed authentications, session termination, and
    /// bulk session revocation).
    /// </summary>
    /// <remarks>
    /// <para>All methods have default no-op implementations — implement only the events you care about.</para>
    /// <para>Hooks are invoked after the operation completes and before the response is written,
    /// in the request scope (when triggered from an HTTP endpoint), so implementations may freely access request-scoped services.</para>
    /// <para>Implementations must not throw. Exceptions from an audit logger will propagate out of the caller
    /// and turn an otherwise-successful auth operation into a 500 (when triggered from an HTTP endpoint).</para>
    /// <para>None of the result types carry a raw password or a raw API key, so failure records are safe to serialise.
    /// On the success path, however, <see cref="LoginResult"/> and <see cref="ApiKeyAuthResult"/> embed an
    /// <see cref="AuthResponse"/> that carries the issued JWT and (when refresh tokens are enabled) the raw refresh token —
    /// both are bearer credentials. Implementations should log metadata (<c>Success</c>, attempted-subject, failure reason, IP)
    /// rather than the whole result; in particular, never pass a result to a structured logger's destructuring syntax
    /// (e.g. Serilog <c>{@result}</c>) or to <c>JsonSerializer.Serialize</c>, because that would write the access token
    /// and refresh token to log storage.</para>
    /// <para>
    /// <b>Register as singleton or scoped.</b> <see cref="RefreshTokenService"/> captures the logger at construction
    /// time, so the logger must outlive the service that captured it. The library registers <see cref="IRefreshTokenService"/>
    /// as scoped, which means a scoped or singleton logger works correctly. Transient is wasteful but harmless. Avoid any
    /// lifetime shorter than <see cref="IRefreshTokenService"/>'s — the only realistic way to trip this is to re-register
    /// <see cref="IRefreshTokenService"/> as singleton while keeping a scoped logger, which would cause the service to cache
    /// one scope's logger for its entire lifetime.
    /// </para>
    /// <para>
    /// <b>Hook placement</b>:
    /// <see cref="OnRefreshAsync"/>, <see cref="OnLogoutAsync"/>, and <see cref="OnSessionsInvalidatedAsync"/> are invoked
    /// from inside <see cref="RefreshTokenService"/>, so both HTTP-endpoint and programmatic callers trigger the hook.
    /// <see cref="OnLoginAsync"/> and <see cref="OnApiKeyAuthAsync"/> are invoked from the HTTP endpoint layer only, because
    /// the underlying <see cref="IAuthRequestValidationService"/> is consumer-implemented and cannot be guaranteed to call
    /// the audit logger itself. Consumers who validate credentials outside the HTTP endpoint flow can resolve
    /// <see cref="IAuthAuditLogger"/> from DI and invoke <see cref="OnLoginAsync"/> / <see cref="OnApiKeyAuthAsync"/>
    /// themselves — the library's endpoint code uses the interface exactly the same way.
    /// </para>
    /// <para>
    /// <b>Default-interface-method caveat</b>: the no-op defaults only resolve when a method is invoked through the
    /// <see cref="IAuthAuditLogger"/> interface. Calling an unimplemented method directly on the concrete class (e.g.
    /// <c>new MyLogger().OnLoginAsync(...)</c>) will fail to compile because the class does not provide an implementation.
    /// Always pass an instance as <see cref="IAuthAuditLogger"/>, or have the class implement every method explicitly if
    /// you need to invoke them on the concrete type.
    /// </para>
    /// </remarks>
    public interface IAuthAuditLogger
    {
        /// <summary>
        /// Invoked after a username/password login attempt. <paramref name="result"/> carries
        /// success state, the attempted subject, and (on failure) a <see cref="LoginFailureReason"/>.
        /// </summary>
        /// <param name="httpContext">The HTTP context of the request that triggered the event.</param>
        /// <param name="result">The structured result of the login validation.</param>
        Task OnLoginAsync(HttpContext httpContext, LoginResult result) => Task.CompletedTask;

        /// <summary>
        /// Invoked after an API key authentication attempt. <paramref name="result"/> carries
        /// success state, the attempted client identifier, and (on failure) an <see cref="ApiKeyAuthFailureReason"/>.
        /// </summary>
        /// <param name="httpContext">The HTTP context of the request that triggered the event.</param>
        /// <param name="result">The structured result of the API key validation.</param>
        Task OnApiKeyAuthAsync(HttpContext httpContext, ApiKeyAuthResult result) => Task.CompletedTask;

        /// <summary>
        /// Invoked after a refresh token operation. <paramref name="result"/> carries success state,
        /// the subject and family identifier (when known), and (on failure) a <see cref="RefreshFailureReason"/>.
        /// A <see cref="RefreshFailureReason.TheftDetected"/> result is particularly security-relevant.
        /// </summary>
        /// <param name="httpContext">
        /// The HTTP context of the request that triggered the event, or <c>null</c> when the refresh was
        /// invoked programmatically (e.g. from a background service). Implementations should guard against null
        /// when recording IP or User-Agent data.
        /// </param>
        /// <param name="result">The structured result of the refresh operation.</param>
        Task OnRefreshAsync(HttpContext? httpContext, RefreshResult result) => Task.CompletedTask;

        /// <summary>
        /// Invoked after a logout operation. <paramref name="result"/> reports whether the supplied
        /// token matched a stored family and, if so, the subject and family identifier that were invalidated.
        /// </summary>
        /// <param name="httpContext">
        /// The HTTP context of the request that triggered the event, or <c>null</c> when the logout was
        /// invoked programmatically (e.g. from a background service). Implementations should guard against null
        /// when recording IP or User-Agent data.
        /// </param>
        /// <param name="result">The structured result of the logout operation.</param>
        Task OnLogoutAsync(HttpContext? httpContext, LogoutResult result) => Task.CompletedTask;

        /// <summary>
        /// Invoked after a bulk session revocation (programmatic call to
        /// <see cref="IRefreshTokenService.InvalidateAllSessionsAsync"/>). Not tied to an HTTP request —
        /// consumers should log timestamp and any caller-side context (e.g. admin identity) themselves.
        /// </summary>
        /// <param name="result">The structured result of the bulk revocation.</param>
        Task OnSessionsInvalidatedAsync(SessionRevocationResult result) => Task.CompletedTask;

        /// <summary>
        /// Invoked when creating a new refresh-token family (a login) revoked one or more existing
        /// sessions because the service is configured with <see cref="ConcurrentSessionPolicy.SingleSession"/>.
        /// Fired from inside <see cref="IRefreshTokenService.CreateRefreshTokenAsync"/> — so both HTTP-endpoint
        /// and programmatic logins trigger it — and only when at least one family was actually revoked.
        /// </summary>
        /// <remarks>
        /// Deliberately distinct from <see cref="OnSessionsInvalidatedAsync"/>: that hook reports an explicit
        /// bulk revocation (password change, role demotion, admin-forced logout), whereas this one reports the
        /// automatic revoke-others-on-login enforcement, so consumers can record it as its own audit event
        /// (e.g. a "concurrent session revoked" security event). Not tied to an HTTP request — the creating
        /// call carries no <see cref="HttpContext"/> — so consumers should log timestamp and any caller-side
        /// context themselves.
        /// </remarks>
        /// <param name="result">The subject and the number of prior sessions revoked by the new login.</param>
        Task OnConcurrentSessionsRevokedAsync(SessionRevocationResult result) => Task.CompletedTask;
    }
}
