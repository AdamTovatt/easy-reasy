using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Optional service that receives structured notifications for every authentication event
    /// the library surfaces. Register an implementation in DI to emit ISO 27001 A.12.4.1 audit
    /// records (successful authentications, failed authentications, second-factor enrollment, second-factor
    /// verification, session termination, and bulk session revocation).
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
    /// <see cref="OnLoginAsync"/>, <see cref="OnApiKeyAuthAsync"/>, and <see cref="OnExternalAuthAsync"/> are invoked from
    /// the HTTP endpoint layer only (the last from a provider integration package such as <c>EasyReasy.Auth.Google</c>),
    /// because the underlying credential validation is consumer-implemented and cannot be guaranteed to call the audit
    /// logger itself. Consumers who authenticate outside the built-in HTTP endpoint flow can resolve
    /// <see cref="IAuthAuditLogger"/> from DI and invoke these hooks themselves — the library's endpoint code uses the
    /// interface exactly the same way.
    /// <see cref="OnWebAuthnRegistrationAsync"/> and <see cref="OnWebAuthnAuthenticationAsync"/> go one step further and
    /// are invoked by the consumer only: the WebAuthn ceremonies run partly in the browser, so this library ships no
    /// endpoint for either and <see cref="WebAuthnVerifier"/> is a pure function that takes neither an
    /// <see cref="HttpContext"/> nor a logger.
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
        /// Invoked after an external identity-provider authentication attempt (for example a Google sign-in).
        /// <paramref name="result"/> carries success state, the <see cref="ExternalAuthResult.Provider"/> name,
        /// the attempted subject, and (on failure) an <see cref="ExternalAuthFailureReason"/>.
        /// </summary>
        /// <remarks>
        /// Like <see cref="OnLoginAsync"/> and <see cref="OnApiKeyAuthAsync"/>, this hook is invoked from the
        /// HTTP endpoint layer — here, a provider integration package's endpoint (such as
        /// <c>EasyReasy.Auth.Google</c>'s <c>AddGoogleAuthEndpoint</c>) — rather than from inside the core
        /// services, because the identity decision is made by consumer-supplied code that the core cannot
        /// guarantee will call the logger itself.
        /// </remarks>
        /// <param name="httpContext">The HTTP context of the request that triggered the event.</param>
        /// <param name="result">The structured result of the external authentication attempt.</param>
        Task OnExternalAuthAsync(HttpContext httpContext, ExternalAuthResult result) => Task.CompletedTask;

        /// <summary>
        /// Invoked after a WebAuthn registration ceremony is verified — a user enrolling a security key,
        /// Touch ID or Face ID as a second factor. <paramref name="result"/> carries success state and,
        /// on failure, a <see cref="WebAuthnRegistrationFailureReason"/> naming the check that rejected it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Like <see cref="OnLoginAsync"/> and <see cref="OnExternalAuthAsync"/>, this hook is not invoked
        /// from inside the core: <see cref="WebAuthnVerifier"/> is a pure function with no HTTP context and
        /// no DI, and the endpoint that runs the ceremony is the consumer's. Resolve
        /// <see cref="IAuthAuditLogger"/> and call this after verifying.
        /// </para>
        /// <para>
        /// The distinctions in <see cref="WebAuthnRegistrationFailureReason"/> exist for this record. A
        /// ceremony rejected for its origin is a page running somewhere it should not be; one rejected for
        /// a challenge is a stale or replayed enrollment; one rejected for a cleared user-verification flag
        /// is a policy the authenticator could not meet. Logging the reason keeps them apart.
        /// <see cref="WebAuthnRegistrationResult.FailureMessage"/> may be recorded alongside it — it names
        /// structures rather than values, and the values it does quote are bounded and free of control
        /// characters.
        /// </para>
        /// <para>
        /// Unlike <see cref="LoginResult"/> and <see cref="ApiKeyAuthResult"/>, this result carries no
        /// bearer credential: a verified registration yields a public key, so the serialisation caveat
        /// above does not apply to it.
        /// </para>
        /// </remarks>
        /// <param name="httpContext">The HTTP context of the request that triggered the event.</param>
        /// <param name="result">The structured result of the registration verification.</param>
        Task OnWebAuthnRegistrationAsync(HttpContext httpContext, WebAuthnRegistrationResult result) => Task.CompletedTask;

        /// <summary>
        /// Invoked after a WebAuthn authentication ceremony is verified — a user presenting the security
        /// key, Touch ID or Face ID they enrolled. <paramref name="result"/> carries success state and, on
        /// failure, a <see cref="WebAuthnAuthenticationFailureReason"/> naming the check that rejected it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Invoked by the consumer only, for the same reason as
        /// <see cref="OnWebAuthnRegistrationAsync"/>: <see cref="WebAuthnVerifier"/> is a pure function
        /// with no <see cref="HttpContext"/> and no DI, and the endpoint that runs the ceremony is the
        /// consumer's.
        /// </para>
        /// <para>
        /// The record an intrusion is most likely to show up in. A failed second factor is an attempt to
        /// finish a login that already passed a first factor, so the reason matters:
        /// <see cref="WebAuthnAuthenticationFailureReason.SignatureInvalid"/> is something answering
        /// without the private key, <see cref="WebAuthnAuthenticationFailureReason.ChallengeMismatch"/> a
        /// replayed assertion, and
        /// <see cref="WebAuthnAuthenticationFailureReason.SignCounterRegressed"/> WebAuthn's one signal of
        /// a cloned credential — evidence worth alerting on rather than counting.
        /// <see cref="WebAuthnAuthenticationResult.FailureMessage"/> may be recorded alongside it, on the
        /// same terms as the registration hook's.
        /// </para>
        /// <para>
        /// Carries no bearer credential: a verified assertion yields a sign counter and two flags, so the
        /// serialisation caveat above does not apply to it.
        /// </para>
        /// </remarks>
        /// <param name="httpContext">The HTTP context of the request that triggered the event.</param>
        /// <param name="result">The structured result of the authentication verification.</param>
        Task OnWebAuthnAuthenticationAsync(HttpContext httpContext, WebAuthnAuthenticationResult result) => Task.CompletedTask;

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

        /// <summary>
        /// Invoked when a specific prior refresh-token family is retired because a re-issue superseded it —
        /// the targeted counterpart to <see cref="IRefreshTokenService.RetireFamilyAsync"/>. Fired from inside
        /// <see cref="RefreshTokenService"/> whenever a non-empty family id is retired, so both HTTP-endpoint
        /// and programmatic callers trigger it.
        /// </summary>
        /// <remarks>
        /// Deliberately distinct from <see cref="OnLogoutAsync"/> (an actual logout) and from
        /// <see cref="OnConcurrentSessionsRevokedAsync"/> (global single-session enforcement that kills the
        /// subject's other sessions): this one means "this one prior session was superseded by a re-issue,"
        /// leaving the subject's other sessions untouched. Because the underlying store call reports neither
        /// existence nor a subject, the record means "a retire was requested for this family."
        /// </remarks>
        /// <param name="httpContext">
        /// The HTTP context of the request that triggered the event, or <c>null</c> when invoked programmatically
        /// (e.g. from a background service). Implementations should guard against null when recording IP or
        /// User-Agent data.
        /// </param>
        /// <param name="result">The retired family id and, when the caller supplied it, the subject.</param>
        Task OnSessionSupersededAsync(HttpContext? httpContext, FamilyRetirementResult result) => Task.CompletedTask;
    }
}
