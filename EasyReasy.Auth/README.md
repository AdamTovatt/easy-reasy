# EasyReasy.Auth

[← Back to EasyReasy System](../README.md)

[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.Auth-blue.svg)](https://www.nuget.org/packages/EasyReasy.Auth)

A lightweight .NET library for internal JWT authentication and claims handling, designed for simplicity and security.

## Overview

EasyReasy.Auth makes it easy to issue, validate, and work with JWT tokens in your .NET applications, with built-in support for roles, custom claims, and progressive brute-force protection.

**Why Use EasyReasy.Auth?**

- **Simple JWT issuing**: Create signed tokens with standard and custom claims
- **Claims injection**: Access user and tenant IDs easily in your controllers
- **Role access**: Retrieve all roles for the current user with a single call
- **Claim access**: Retrieve any claim value by key or enum with a single call
- **Progressive delay**: Built-in middleware to slow down brute-force attacks (enabled by default)
- **Refresh token rotation**: Opt-in refresh tokens with automatic theft detection via token family tracking
- **MFA primitives**: RFC 6238 TOTP generator (secret generation, `otpauth://` provisioning URI, code validation), RFC 4648 base32 codec, and AES-256-GCM secret cipher — storage-agnostic building blocks for time-based one-time-password and encrypt-at-rest flows
- **Flexible configuration**: Options pattern for JWT settings (issuer, audience, clock skew) and progressive delay tuning
- **Clear error messages**: Enforces minimum secret length for security

## Quick Start

### 1. Add to your project

Install via NuGet:
```sh
# In your web/API project
dotnet add package EasyReasy.Auth
dotnet add package Microsoft.IdentityModel.JsonWebTokens
```
> Important note! You will always get 401 Unauthorized if you forget to install `Microsoft.IdentityModel.JsonWebTokens`

### 2. Configure in Program.cs

```csharp
string jwtSecret = Environment.GetEnvironmentVariable("JWT_SIGNING_SECRET")!;
builder.Services.AddEasyReasyAuth(jwtSecret, options =>
{
    options.Issuer = "my-issuer";
});

WebApplication app = builder.Build();
app.UseEasyReasyAuth(); // Progressive delay enabled by default
```

### 3. Issue tokens

#### Option A: Manual Token Creation **(Not recommended, see Option B for recommended way)**
> You probably want to get an instance of IJWtTokenService via dependency injection in your controller class and create an endpoint in that is responsible for issuing tokens if they should be issued.
```csharp
IJwtTokenService tokenService = new JwtTokenService(jwtSecret, issuer: "my-issuer", audience: "my-api");
string token = tokenService.CreateToken(
    subject: "user-123",
    authType: "apikey",
    additionalClaims: new[] { new Claim("tenant_id", "tenant-42") },
    roles: new[] { "admin", "user" },
    expiresAt: DateTime.UtcNow.AddHours(1));
```

#### Option B: Automatic Auth Endpoints (Recommended)
The library can automatically create authentication endpoints for you. First, implement the validation service:

```csharp
public class MyAuthService : IAuthRequestValidationService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public MyAuthService(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<ApiKeyAuthResult> ValidateApiKeyRequestAsync(ApiKeyAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
    {
        // Validate API key (e.g., check database, external service, etc.)
        User? user = await _userRepository.GetByApiKeyAsync(request.ApiKey);
        if (user == null)
        {
            return ApiKeyAuthResult.Failed(ApiKeyAuthFailureReason.UnknownKey);
        }

        // Extract tenant ID from header if available
        string? tenantId = user.TenantId;
        if (httpContext?.Request.Headers.TryGetValue("X-Tenant-ID", out StringValues headerTenantId) == true)
        {
            tenantId = headerTenantId.ToString();
        }

        // Create JWT token — only include the tenant_id claim if we actually have a value.
        List<Claim> claims = new List<Claim>();
        if (tenantId != null)
        {
            claims.Add(new Claim("tenant_id", tenantId));
        }

        DateTime expiresAt = DateTime.UtcNow.AddHours(1);
        string token = jwtTokenService.CreateToken(
            subject: user.Id,
            authType: "apikey",
            additionalClaims: claims,
            roles: user.Roles.ToArray(),
            expiresAt: expiresAt);

        return ApiKeyAuthResult.Succeeded(new AuthResponse(token, expiresAt.ToString("o")), user.Id);
    }

    public async Task<LoginResult> ValidateLoginRequestAsync(LoginAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
    {
        // Validate username/password
        User? user = await _userRepository.GetByUsernameAsync(request.Username);
        if (user == null)
        {
            // Populate AttemptedSubject so audit logs can attribute the failed attempt.
            return LoginResult.Failed(LoginFailureReason.UnknownUser, attemptedSubject: request.Username);
        }

        if (!_passwordHasher.ValidatePassword(request.Password, user.PasswordHash))
        {
            return LoginResult.Failed(LoginFailureReason.InvalidCredentials, attemptedSubject: user.Id);
        }

        // Extract tenant ID from header if available
        string? tenantId = user.TenantId;
        if (httpContext?.Request.Headers.TryGetValue("X-Tenant-ID", out StringValues headerTenantId) == true)
        {
            tenantId = headerTenantId.ToString();
        }

        // Create JWT token — only include the tenant_id claim if we actually have a value.
        List<Claim> claims = new List<Claim>();
        if (tenantId != null)
        {
            claims.Add(new Claim("tenant_id", tenantId));
        }

        DateTime expiresAt = DateTime.UtcNow.AddHours(1);
        string token = jwtTokenService.CreateToken(
            subject: user.Id,
            authType: "user",
            additionalClaims: claims,
            roles: user.Roles.ToArray(),
            expiresAt: expiresAt);

        return LoginResult.Succeeded(new AuthResponse(token, expiresAt.ToString("o")), user.Id);
    }
}
```

Then register the service and add endpoints in `Program.cs`. Here's a complete setup example:
```csharp
string jwtSecret = Environment.GetEnvironmentVariable("JWT_SIGNING_SECRET")!;

// 1. Register authentication
builder.Services.AddEasyReasyAuth(jwtSecret, options =>
{
    options.Issuer = "my-issuer";
});

// 2. Register dependencies
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IPasswordHasher, SecurePasswordHasher>();

// 3. Register validation service (use AddScoped for services with database dependencies)
builder.Services.AddScoped<IAuthRequestValidationService, MyAuthService>();

WebApplication app = builder.Build();

// 4. Configure middleware (UseEasyReasyAuth includes UseAuthentication/UseAuthorization)
app.UseEasyReasyAuth();

// 5. Add auth endpoints (resolved from DI automatically per-request)
app.AddAuthEndpoints(
    allowApiKeys: true,
    allowUsernamePassword: true);

app.MapControllers();
```

**Note:** `IAuthRequestValidationService` is resolved from DI per-request when auth endpoints are called. Use `AddScoped` when your validation service has database dependencies.

This will automatically create:
- `POST /api/auth/apikey` - For API key authentication
- `POST /api/auth/login` - For username/password authentication

Both endpoints return:
- `200 OK` with `AuthResponse` (token, expiration, and optional refresh token) on success
- `400 Bad Request` with a validation problem when a required credential field is absent or empty, keyed by the wire field names the endpoint publishes (`username`, `password`, `apiKey`). See "Requests missing a credential" below.
- `401 Unauthorized` on invalid credentials

**Requests missing a credential.** A request with an absent or empty `username`, `password` or `apiKey` never becomes an authentication attempt, so it is answered as a `400`, not the `401` that means "wrong credentials" — including when it supplies one field and omits the other, such as a `username` with no `password`. `IAuthRequestValidationService` is not called on that path. A whitespace-only value is a value the caller supplied, so it goes on to the validation service and takes the ordinary 401. A body that fails to bind at all (empty, unparseable, or a bare `null`) is rejected by the framework before the endpoint runs, so it gets a bare `400` with no audit row and no `Cache-Control` header.

### 4. Accessing HTTP Context in Validation

The `IAuthRequestValidationService` methods receive an optional `HttpContext` parameter, allowing you to access request headers, query parameters, and other HTTP context information during authentication. This is particularly useful for multi-tenant applications.

**Example: Extracting Tenant ID from Headers**
```csharp
public async Task<ApiKeyAuthResult> ValidateApiKeyRequestAsync(ApiKeyAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
{
    // Validate API key
    User? user = await _userRepository.GetByApiKeyAsync(request.ApiKey);
    if (user == null)
    {
        return ApiKeyAuthResult.Failed(ApiKeyAuthFailureReason.UnknownKey);
    }

    // Extract tenant ID from header
    string? tenantId = null;
    if (httpContext?.Request.Headers.TryGetValue("X-Tenant-ID", out StringValues headerTenantId) == true)
    {
        tenantId = headerTenantId.ToString();
    }

    // Only include the tenant_id claim if we have a value — avoid writing empty-string claims.
    List<Claim> claims = new List<Claim>();
    if (tenantId != null)
    {
        claims.Add(new Claim("tenant_id", tenantId));
    }

    DateTime expiresAt = DateTime.UtcNow.AddHours(1);
    string token = jwtTokenService.CreateToken(
        subject: user.Id,
        authType: "apikey",
        additionalClaims: claims,
        roles: user.Roles.ToArray(),
        expiresAt: expiresAt);

    return ApiKeyAuthResult.Succeeded(new AuthResponse(token, expiresAt.ToString("o")), user.Id);
}
```

**Example: Accessing Query Parameters**
```csharp
public async Task<LoginResult> ValidateLoginRequestAsync(LoginAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
{
    // Validate credentials
    User? user = await _userRepository.GetByUsernameAsync(request.Username);
    if (user == null)
    {
        return LoginResult.Failed(LoginFailureReason.UnknownUser, attemptedSubject: request.Username);
    }

    if (!_passwordHasher.ValidatePassword(request.Password, user.PasswordHash))
    {
        return LoginResult.Failed(LoginFailureReason.InvalidCredentials, attemptedSubject: user.Id);
    }

    // Extract organization from query parameter
    string? organization = httpContext?.Request.Query["org"].ToString();

    // Only include claims for values we actually have.
    List<Claim> claims = new List<Claim>();
    if (user.TenantId != null)
    {
        claims.Add(new Claim("tenant_id", user.TenantId));
    }
    if (organization != null)
    {
        claims.Add(new Claim("organization", organization));
    }

    DateTime expiresAt = DateTime.UtcNow.AddHours(1);
    string token = jwtTokenService.CreateToken(
        subject: user.Id,
        authType: "user",
        additionalClaims: claims,
        roles: user.Roles.ToArray(),
        expiresAt: expiresAt);

    return LoginResult.Succeeded(new AuthResponse(token, expiresAt.ToString("o")), user.Id);
}
```

**Note:** The `HttpContext` parameter is optional and defaults to `null`, so implementations that don't need HTTP context can simply omit it.

### 5. Access claims and roles in controllers

```csharp
string? userId = HttpContext.GetUserId();
string? tenantId = HttpContext.GetTenantId();
IEnumerable<string> roles = HttpContext.GetRoles();
string? email = HttpContext.GetClaimValue("email");

// Type-safe claim access using the EasyReasyClaim enum
string? userId2 = HttpContext.GetClaimValue(EasyReasyClaim.UserId);
string? tenantId2 = HttpContext.GetClaimValue(EasyReasyClaim.TenantId);
string? issuer = HttpContext.GetClaimValue(EasyReasyClaim.Issuer);

// The refresh token family id rides on the access token as the "family_id" claim (see Section 9).
string? familyId = HttpContext.GetRefreshFamilyId();
string? familyId2 = HttpContext.GetClaimValue(EasyReasyClaim.RefreshFamilyId);
```

### 6. Password Hashing

The library includes a secure password hasher using PBKDF2 with HMAC-SHA512. The `IPasswordHasher` interface provides these methods:

```csharp
public interface IPasswordHasher
{
    string HashPassword(string password);
    bool ValidatePassword(string password, string passwordHash);
}
```

Use it in your `IAuthRequestValidationService` implementation (see the main example above for a complete implementation with constructor injection). Register the password hasher in `Program.cs`:

```csharp
builder.Services.AddSingleton<IPasswordHasher, SecurePasswordHasher>();
```

**Key Features:**
- Uses PBKDF2 with HMAC-SHA512 and 100,000 iterations
- 128-bit cryptographic random salt per hash
- Maximum password length enforcement (1024 UTF-8 bytes) to prevent CPU DoS
- Minimum iteration count enforcement during verification to reject tampered hashes
- Constant-time comparison to prevent timing attacks

### 7. Password Reset Tokens

The library provides a secure password reset token handler for implementing password reset flows. The handler manages the cryptographic operations; you are responsible for storage, expiration enforcement, and delivery (e.g., email).

```csharp
public interface IPasswordResetTokenHandler
{
    PasswordResetToken GenerateResetToken();
    bool ValidateResetToken(string token, string storedTokenHash);
}

public readonly struct PasswordResetToken
{
    public required string Token { get; init; }      // base64url, send to user via email
    public required string TokenHash { get; init; }  // SHA-256 hash, store in database
}
```

Register in `Program.cs`:
```csharp
builder.Services.AddPasswordResetTokenHandler();
// Or manually: builder.Services.AddSingleton<IPasswordResetTokenHandler, SecurePasswordResetTokenHandler>();
```

**Usage example:**
```csharp
// User requests a password reset
PasswordResetToken resetToken = _tokenHandler.GenerateResetToken();
await _db.StoreResetRequest(user.Id, resetToken.TokenHash, DateTime.UtcNow);
await _emailService.SendResetEmail(user.Email, resetToken.Token);

// User returns with the token from the email
ResetRequest request = await _db.GetResetRequest(userId);
if (request.CreatedAt.AddHours(1) < DateTime.UtcNow)
    return "expired"; // expiration is your responsibility

if (!_tokenHandler.ValidateResetToken(incomingToken, request.TokenHash))
    return "invalid";

// Token is valid — set new password
user.PasswordHash = _passwordHasher.HashPassword(newPassword);
await _db.Save(user);
```

**Key Features:**
- 256-bit cryptographically random tokens (base64url-encoded)
- SHA-256 hashing for storage (never store plaintext tokens)
- Stateless and thread-safe (registered as singleton)

### 8. Refresh Tokens

EasyReasy.Auth supports refresh token rotation with automatic theft detection via token family tracking. The library is database-agnostic — you implement `IRefreshTokenStore` to persist tokens however you like.

#### Setup

1. **Implement `IRefreshTokenStore`** to connect to your database:
```csharp
public class MyRefreshTokenStore : IRefreshTokenStore
{
    public Task StoreAsync(StoredRefreshToken refreshToken, CancellationToken cancellationToken = default) { /* INSERT into DB */ }
    public Task<StoredRefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) { /* SELECT by hash */ }
    public Task<bool> MarkAsConsumedAsync(string tokenHash, DateTime consumedAt, CancellationToken cancellationToken = default) { /* see note below */ }
    public Task InvalidateFamilyAsync(string familyId, CancellationToken cancellationToken = default) { /* UPDATE SET invalidated WHERE family_id = ... */ }
    public Task<int> InvalidateAllFamiliesForUserAsync(string subject, CancellationToken cancellationToken = default) { /* UPDATE SET invalidated WHERE subject = ... AND invalidated = false; return affected-family count */ }
}
```

2. **Register in `Program.cs`:**
```csharp
builder.Services.AddRefreshTokenService<MyRefreshTokenStore>(
    refreshTokenLifetime: TimeSpan.FromDays(30),  // default
    accessTokenLifetime: TimeSpan.FromHours(1),   // default
    concurrentSessionPolicy: ConcurrentSessionPolicy.AllowMultiple);  // default — see Section 9 for single-session enforcement

// Enable the refresh endpoint alongside your auth endpoints
app.AddAuthEndpoints(allowRefresh: true);
// Or standalone: app.AddRefreshEndpoint();
```
This creates `POST /api/auth/refresh` which accepts `{ "refreshToken": "..." }` and returns a new access + refresh token pair.

The logout endpoint (`POST /api/auth/logout`) is enabled by default when you call `AddAuthEndpoints` — see the Logout section below.

3. **Issue refresh tokens in your validation service** by injecting `IRefreshTokenService` alongside whatever user repository / password hasher you already use:
```csharp
public class MyAuthService : IAuthRequestValidationService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;

    public MyAuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<LoginResult> ValidateLoginRequestAsync(
        LoginAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
    {
        User? user = await _userRepository.GetByUsernameAsync(request.Username);
        if (user == null)
        {
            // Populate AttemptedSubject so audit logs can attribute the failed attempt.
            return LoginResult.Failed(LoginFailureReason.UnknownUser, attemptedSubject: request.Username);
        }

        if (!_passwordHasher.ValidatePassword(request.Password, user.PasswordHash))
        {
            return LoginResult.Failed(LoginFailureReason.InvalidCredentials, attemptedSubject: user.Id);
        }

        // ... create access token (see the main example) ...

        string refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(
            subject: user.Id,
            authType: "user",
            serializedClaims: RefreshTokenClaims.SerializeClaims(claims),
            serializedRoles: RefreshTokenClaims.SerializeRoles(roles));

        return LoginResult.Succeeded(new AuthResponse(token, expiresAt.ToString("o"), refreshToken), user.Id);
    }
}
```

> **Producing the serialized strings.** `CreateRefreshTokenAsync` takes the claims and roles as already-serialized JSON. Use `RefreshTokenClaims.SerializeClaims(claims)` and `RefreshTokenClaims.SerializeRoles(roles)` to produce them — they emit the exact format the refresh path round-trips, so any claim you seed this way survives a refresh (subject to your `IRefreshClaimsResolver`, if registered).

#### How It Works

- Refresh tokens are 32-byte cryptographic random strings, stored as SHA-256 hashes
- Each token belongs to a **family** — when a token is used, a new one is issued in the same family (rotation)
- If a token that was already used gets presented again, the library detects theft and **invalidates the entire family**
- Everything is opt-in: you must register the service, enable the endpoint, and inject `IRefreshTokenService` in your validation service

#### Mid-session re-evaluation: `IRefreshClaimsResolver`

By default, the refresh path inherits the claims and roles from the previous token in the family. That works fine for stable sessions, but it does not let policy changes ride into the new access token: a password that expires mid-session, a role that gets revoked, an account that gets disabled — none of these reach the user until they log in again.

Register an `IRefreshClaimsResolver` and the library calls it on every refresh, before the atomic consume. The resolver either returns the claims and roles to put on the new tokens (replacing what was stored) or denies the refresh outright with `RefreshFailureReason.DeniedByResolver`. When no resolver is registered, refresh behaviour is identical to today.

The intended pattern is **re-derive from the current user state on every refresh**, not "take what's stored and patch it." Anything the resolver outputs replaces the stored claims and roles on both the new access token and the new stored refresh row, so any value you want to ride forward must be returned each time.

```csharp
public class PolicyAwareRefreshResolver : IRefreshClaimsResolver
{
    private readonly IUserRepository _users;

    public PolicyAwareRefreshResolver(IUserRepository users) { _users = users; }

    public async Task<RefreshClaimsDecision> ResolveAsync(
        RefreshClaimsContext context, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(context.Subject, cancellationToken);
        if (user == null || user.IsDisabled)
        {
            return RefreshClaimsDecision.Deny();
        }

        // Build claims fresh from the current user, not from context.StoredClaims —
        // re-deriving each refresh is the whole point of the resolver. (Use
        // context.StoredClaims only for facts that genuinely cannot be re-derived.)
        List<Claim> claims = new List<Claim>
        {
            new Claim("tenant_id", user.TenantId),
            new Claim("email", user.Email),
        };
        if (user.PasswordExpiresAt <= DateTime.UtcNow)
        {
            claims.Add(new Claim("pwd_expired", "true"));
        }

        return RefreshClaimsDecision.Allow(claims, user.CurrentRoles);
    }
}

// Program.cs — register before AddRefreshTokenService<TStore>
builder.Services.AddScoped<IRefreshClaimsResolver, PolicyAwareRefreshResolver>();
builder.Services.AddRefreshTokenService<MyRefreshTokenStore>();
```

A few contract notes worth knowing:

- **Resolver runs before the atomic consume.** A `Deny()` or a thrown resolver does **not** burn the stored refresh token. The client can retry once the consumer fixes the underlying issue. Theft detection is unchanged — it still fires at the atomic consume on the legitimate winning request.
- **Throws propagate, with an audit row first.** If your resolver fails (DB blip, transient error), the exception bubbles out of `RefreshAsync` — but before it propagates, the audit logger receives a synthetic `RefreshFailureReason.ResolverError` result so the audit trail remains the authoritative record of every refresh outcome (ISO 27001 A.12.4.1). Consumers that prefer a graceful denial without surfacing an exception should catch internally and return `RefreshClaimsDecision.Deny()`.
- **Side effects must be idempotent.** Even on `Allow`, the subsequent atomic consume can still fail (concurrent reuse → `TheftDetected`). Any side effects the resolver already committed will persist.
- **Keep it fast.** The resolver runs inside the read-then-consume window. A slow resolver widens the race window in which two legitimate near-simultaneous refreshes trip theft detection. Cache hot lookups; avoid per-refresh network calls.
- **`DeniedByResolver` flows through `IAuthAuditLogger.OnRefreshAsync`** like any other refresh failure — your existing audit pipeline picks it up automatically.

#### Important: `MarkAsConsumedAsync` Must Be Atomic

`MarkAsConsumedAsync` returns `bool` — it must return `true` only for the **first** caller and `false` for any concurrent requests that try to consume the same token. This prevents a race condition where two simultaneous requests both redeem the same refresh token before either marks it consumed.

In SQL, use a conditional update and check affected rows:
```csharp
public async Task<bool> MarkAsConsumedAsync(string tokenHash, DateTime consumedAt, CancellationToken cancellationToken = default)
{
    // Only updates if consumed_at is still NULL — returns true if 1 row was affected
    int affected = await db.ExecuteAsync(
        "UPDATE refresh_tokens SET consumed_at = @consumedAt WHERE token_hash = @tokenHash AND consumed_at IS NULL",
        new { tokenHash, consumedAt });
    return affected == 1;
}
```

For other stores (Redis, MongoDB, etc.), use the equivalent atomic compare-and-set operation. If your store cannot guarantee atomicity, concurrent refresh requests could both succeed, issuing duplicate token pairs.

### 9. Logout and Bulk Session Revocation

**Logout endpoint (`POST /api/auth/logout`)** — revokes the refresh token family for a supplied token so that even a captured refresh token can no longer mint new access tokens.

- Enabled by default when you call `AddAuthEndpoints`. Because it's on by default you **must** register `IRefreshTokenService` (e.g. `builder.Services.AddRefreshTokenService<MyStore>()`) — otherwise `AddAuthEndpoints` throws at startup. Disable with `allowLogout: false` if you don't need logout.
- Accepts `{ "refreshToken": "..." }` (same shape as `/refresh`).
- Anonymous — no access token required. This makes logout-on-expired-access-token still work.
- Always returns `204 No Content`, even for unknown, null, or already-invalidated tokens — the response body does not reveal whether the token was known to the server. A theoretical timing side channel exists (a hit performs one extra write) but with 256-bit random tokens it is not a practical attack surface.
- Stateless JWT access tokens remain valid until their `exp`. If you need immediate access-token invalidation, shorten the access token lifetime (e.g. 15 minutes) — that is the accepted industry trade-off and keeps the library design clean. Adding a JWT revocation list is explicitly out of scope.
- **Anonymous logout trade-off**: because the endpoint requires only a refresh token, anyone who obtains any refresh token from a given family (including an expired or already-consumed one, e.g. from logs) can force-log-out that session. This is a deliberate choice — requiring an access token would block logout when the access token has expired, which is a common case. Callers who need stronger assurance should keep refresh tokens out of logs and treat them as secrets at least as sensitive as access tokens.

```csharp
// The logout endpoint is on by default; allowRefresh must be opted in explicitly.
app.AddAuthEndpoints(allowRefresh: true);
// Or standalone: app.AddLogoutEndpoint();
```

**Bulk session revocation (`IRefreshTokenService.InvalidateAllSessionsAsync`)** — invalidates every refresh token family for a subject at once. Intended for flows where all existing sessions must be kicked:

- Password change
- Role demotion
- Admin-forced logout

```csharp
public class AccountService
{
    private readonly IRefreshTokenService _refreshTokenService;

    public AccountService(IRefreshTokenService refreshTokenService)
    {
        _refreshTokenService = refreshTokenService;
    }

    public async Task ChangePasswordAsync(string userId, string newPassword)
    {
        // ... hash and persist new password ...

        // Kick every existing session for this user.
        await _refreshTokenService.InvalidateAllSessionsAsync(userId);
    }
}
```

The library does not expose an HTTP endpoint for admins to revoke other users' sessions — build your own around `InvalidateAllSessionsAsync` if you need one.

**Single-session enforcement (`ConcurrentSessionPolicy.SingleSession`)** — makes every new login automatically revoke the subject's existing sessions, so only the newest login stays live. Opt in on the service registration:

```csharp
builder.Services.AddRefreshTokenService<MyStore>(
    concurrentSessionPolicy: ConcurrentSessionPolicy.SingleSession);
```

- The default, `ConcurrentSessionPolicy.AllowMultiple`, places no limit on concurrent sessions — each login coexists with any existing ones.
- Under `SingleSession`, `CreateRefreshTokenAsync` invalidates every existing family for the subject before storing the new one. Earlier sessions can no longer refresh, and any still-live access token expires within its (short) lifetime. Use it for "no shared accounts" / single-session requirements such as EU GMP Annex 11 or 21 CFR Part 11 §11.200.
- This is the automatic, per-login counterpart to the manual `InvalidateAllSessionsAsync`. Reach for `SingleSession` when "newest login wins" should be a standing policy; call `InvalidateAllSessionsAsync` for one-off, event-driven kicks (password change, role demotion, admin-forced logout). Both go through the same `IRefreshTokenStore.InvalidateAllFamiliesForUserAsync` primitive and surface the same `SessionRevocationResult` shape.
- When enforcement actually revokes at least one prior session, the service fires `IAuthAuditLogger.OnConcurrentSessionsRevokedAsync` — distinct from `OnSessionsInvalidatedAsync` so you can audit automatic, login-driven revocations separately. See Section 10.
- **Concurrency caveat**: enforcement is two store calls (invalidate, then store), not one transaction. Two logins for the same subject racing concurrently can each miss the other's not-yet-stored family and both stay live. If you need a hard guarantee, serialize concurrent logins for the same subject in your store (a per-subject lock or a unique constraint) — the library cannot, because `IRefreshTokenStore` has no atomic store-and-invalidate-others operation.

**Targeted session supersession on re-issue (`IRefreshTokenService.RetireFamilyAsync`)** — when an endpoint re-mints a fresh token pair for an *already-authenticated* subject (for example an "active organization switch" that re-issues the access token with a changed claim), it mints a **new** refresh-token family while the caller's **prior** family stays live until it expires. To retire *exactly that prior family* as part of the re-mint — and only it, leaving the subject's other devices live — without the misleading `Logout` record that `LogoutAsync` would produce, use `RetireFamilyAsync`:

```csharp
// At the re-issue endpoint (the caller is already authenticated):
string? priorFamilyId = HttpContext.GetRefreshFamilyId();

// ... mint the new pair carrying the changed claim, e.g. via IJwtTokenService.CreateToken +
//     IRefreshTokenService.CreateRefreshTokenAsync (or CreateRefreshTokenWithFamilyAsync if you
//     want the new family id to seed the family_id claim onto this first access token) ...

// Retire the caller's prior family. Other devices stay live; the event is audited as a
// supersession (OnSessionSupersededAsync), not a logout.
await _refreshTokenService.RetireFamilyAsync(priorFamilyId, HttpContext.GetUserId());
```

- **How the prior family is named.** The refresh token family id is surfaced on every access token as the `family_id` claim, readable with `HttpContext.GetRefreshFamilyId()` (or `EasyReasyClaim.RefreshFamilyId`). The claim is injected authoritatively on every refresh from the server-side family id, so it is stable across a family's rotations and cannot be spoofed via stored claims. Exposing it in the signed, client-readable JWT is intended and safe: it is the client's own opaque session id, and possessing it grants no ability to revoke the session — retirement is a server-only operation.
- **`subject` is for the audit row only.** The service cannot derive the subject from a family id, so pass it from the access token (`HttpContext.GetUserId()`). It is recorded on the `FamilyRetirementResult`; it does not affect which family is retired.
- **Null/whitespace is a clean no-op** — no store call, no audit hook, no throw. This is the normal path during rollout: access tokens minted before 5.2.0 carry no `family_id`, so `GetRefreshFamilyId()` returns `null` and `RetireFamilyAsync(null)` simply does nothing. Existing sessions self-heal — each gains the `family_id` claim on its next refresh, no consumer action required.
- This is the targeted, audited counterpart to `IRefreshTokenStore.InvalidateFamilyAsync` — distinct from the global `SingleSession` policy (which kills every other session) and from the bulk `InvalidateAllSessionsAsync`. When a non-empty family id is retired, the service fires `IAuthAuditLogger.OnSessionSupersededAsync`. See Section 10.
- **Assumes `ConcurrentSessionPolicy.AllowMultiple` (the default).** Under `SingleSession`, minting the new pair already revokes the subject's *other* families during creation, so the prior family is gone before `RetireFamilyAsync` runs and "other devices stay live" no longer applies — targeted retirement is redundant there.

### 10. Security Audit Logging (ISO 27001 A.9 / A.12)

Every authentication event the library surfaces — success or failure — is reported through a single optional DI service: `IAuthAuditLogger`. Register an implementation and the built-in endpoints (plus the programmatic triggers `RefreshTokenService.InvalidateAllSessionsAsync` and single-session enforcement on login) will invoke it with a structured result object. Provider integration packages route through the same service: the `EasyReasy.Auth.Google` sign-in endpoint invokes `OnExternalAuthAsync`, so external sign-ins land in the same audit sink as everything else.

| Event | Hook | Control | Typical data to log |
|---|---|---|---|
| Successful / failed username+password login, and a request that carried no credentials (`MissingCredentials`) | `OnLoginAsync(httpContext, LoginResult)` | ISO 27001 A.12.4.1 | outcome, `AttemptedSubject`, `FailureReason`, IP, User-Agent, time |
| Successful / failed API key auth, and a request that carried no key (`MissingKey`) | `OnApiKeyAuthAsync(httpContext, ApiKeyAuthResult)` | ISO 27001 A.12.4.1 | outcome, `AttemptedClientId`, `FailureReason`, IP, User-Agent, time |
| Successful / failed external identity-provider sign-in (e.g. Google) | `OnExternalAuthAsync(httpContext, ExternalAuthResult)` | ISO 27001 A.12.4.1 | outcome, `Provider`, `AttemptedSubject`, `FailureReason`, IP, User-Agent, time |
| Refresh (incl. `TheftDetected`, `DeniedByResolver`, `ResolverError`) | `OnRefreshAsync(httpContext, RefreshResult)` | ISO 27001 A.12.4.1 | outcome, `Subject`, `FamilyId`, `FailureReason`, IP, time |
| Logout | `OnLogoutAsync(httpContext, LogoutResult)` | ISO 27001 A.9.2.6 | `WasKnown`, `Subject`, `FamilyId`, IP, time |
| Bulk session revocation | `OnSessionsInvalidatedAsync(SessionRevocationResult)` | ISO 27001 A.9.2.6 | `Subject`, `InvalidatedFamilyCount`, time |
| Concurrent session revoked on login (`SingleSession` policy) | `OnConcurrentSessionsRevokedAsync(SessionRevocationResult)` | ISO 27001 A.9.2.6 | `Subject`, `InvalidatedFamilyCount`, time |
| Targeted session supersession on re-issue (`RetireFamilyAsync`) | `OnSessionSupersededAsync(httpContext, FamilyRetirementResult)` | ISO 27001 A.9.2.6 | `FamilyId`, `Subject`, IP, time |

All methods have default no-op implementations — implement only the events you care about. The result objects deliberately never carry a raw password or a raw API key, so failure records are safe to serialise to your log store.

⚠️ **Not every reported event is an authentication attempt.** A request missing a required credential field is answered with a `400` (see Section 3), and `OnLoginAsync` / `OnApiKeyAuthAsync` still fire once for it so the attempt stays attributable — with `LoginFailureReason.MissingCredentials` / `ApiKeyAuthFailureReason.MissingKey`, and `Success == false` like any other failure. **A consumer counting failed attempts per account must exclude the reasons that report a request which never became an attempt**, or anyone can lock any account they can name without ever guessing a credential. `AttemptedSubject` / `AttemptedClientId` is populated when the request supplied an identifier and `null` when the identifier is what was missing.

⚠️ **That path is not rate limited.** A `400` neither increments nor clears the progressive-delay failure count, so an anonymous caller can drive audit writes through your `IAuthAuditLogger` as fast as it can post. This is deliberate — a request that never became an attempt should not count as a failed one — but if your audit sink is a database, budget for it or rate limit ahead of the endpoint.

⚠️ **Do not serialise the whole result object on the success path.** `LoginResult`, `ApiKeyAuthResult`, and `ExternalAuthResult` embed an `AuthResponse` that carries the issued JWT access token and (when refresh tokens are enabled) the raw refresh token — both are bearer credentials. Log **metadata** from the result (`Success`, `AttemptedSubject` / `AttemptedClientId`, `FailureReason`) — never log `result.AuthResponse` with a structured logger's destructuring syntax (e.g. Serilog `{@result}`) or `JsonSerializer.Serialize(result)`. The example below follows the right pattern.

```csharp
public class MyAuditLogger : IAuthAuditLogger
{
    private readonly ILogger<MyAuditLogger> _logger;

    public MyAuditLogger(ILogger<MyAuditLogger> logger) { _logger = logger; }

    public Task OnLoginAsync(HttpContext ctx, LoginResult result)
    {
        _logger.LogInformation(
            "auth.login {Outcome} subject={Subject} reason={Reason} ip={Ip}",
            result.Success ? "success" : "failure",
            result.AttemptedSubject,
            result.FailureReason,
            ctx.Connection.RemoteIpAddress);
        return Task.CompletedTask;
    }

    public Task OnRefreshAsync(HttpContext? ctx, RefreshResult result)
    {
        // TheftDetected and DeniedByResolver are particularly worth alerting on —
        // both can indicate a compromised session or a policy state change (password
        // expiry, role revocation, account disable) that just blocked a refresh.
        _logger.LogInformation(
            "auth.refresh {Outcome} subject={Subject} family={Family} reason={Reason}",
            result.Success ? "success" : "failure",
            result.Subject, result.FamilyId, result.FailureReason);
        return Task.CompletedTask;
    }

    public Task OnLogoutAsync(HttpContext? ctx, LogoutResult result)
    {
        if (result.WasKnown)
        {
            _logger.LogInformation(
                "auth.logout subject={Subject} family={Family}",
                result.Subject, result.FamilyId);
        }
        return Task.CompletedTask;
    }

    public Task OnSessionsInvalidatedAsync(SessionRevocationResult result)
    {
        _logger.LogInformation(
            "auth.sessions_revoked subject={Subject} count={Count}",
            result.Subject, result.InvalidatedFamilyCount);
        return Task.CompletedTask;
    }

    public Task OnSessionSupersededAsync(HttpContext? ctx, FamilyRetirementResult result)
    {
        // A specific prior session was retired by a re-issue (see Section 9) — distinct from a
        // logout and from global single-session enforcement.
        _logger.LogInformation(
            "auth.session_superseded subject={Subject} family={Family}",
            result.Subject, result.FamilyId);
        return Task.CompletedTask;
    }
}

// Program.cs
builder.Services.AddSingleton<IAuthAuditLogger, MyAuditLogger>();
```

The audit logger runs in the request scope, so implementations may resolve scoped dependencies. Implementations must not throw — an exception will propagate out of the endpoint and turn a successful auth into a 500.

### 11. Sensitive Request Bodies (do not log)

Four endpoints accept secrets in the request body. Exclude them from any body-logging middleware you run:

| Path | Secret in body |
|---|---|
| `POST /api/auth/login` | password |
| `POST /api/auth/apikey` | API key |
| `POST /api/auth/refresh` | refresh token |
| `POST /api/auth/logout` | refresh token |

The library already redacts these via `ToString()` on the request DTOs (so structured logging that serialises the DTO object is safe). The risk is raw-stream logging that reads the request body before model binding.

Define a shared path-matching helper and reuse it everywhere you exclude sensitive paths. In a top-level `Program.cs` this is a local function; inside a class it should be a `private static` method.

```csharp
// Program.cs (top-level statements) — local function
static bool IsSensitiveAuthPath(PathString path) =>
    path.StartsWithSegments("/api/auth/login") ||
    path.StartsWithSegments("/api/auth/apikey") ||
    path.StartsWithSegments("/api/auth/refresh") ||
    path.StartsWithSegments("/api/auth/logout");
```

**`Microsoft.AspNetCore.HttpLogging`**

```csharp
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.All;
    options.MediaTypeOptions.AddText("application/json");
});

app.UseWhen(
    ctx => !IsSensitiveAuthPath(ctx.Request.Path),
    branch => branch.UseHttpLogging());
```

**Serilog request logging**

```csharp
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diag, ctx) =>
    {
        if (IsSensitiveAuthPath(ctx.Request.Path)) return;
        // only enrich with body contents for non-sensitive paths
    };
});
```

**OpenTelemetry** — ensure any `AspNetCoreInstrumentationOptions.EnrichWithHttpRequest` callback that captures request bodies checks the same path list.

### 12. TOTP and Secret Encryption Primitives

Low-level, storage-agnostic building blocks for multi-factor authentication: an RFC 6238 (TOTP) code generator covering the whole enrollment-to-validation path, a base32 codec, and an AES-256-GCM cipher for holding the shared secret encrypted at rest. They hold nothing — no clock, no persistence, no DI required (the only ambient dependency anywhere is the system CSPRNG the secret and the cipher's nonces come from) — so the enrollment, storage, and replay-protection policy stay entirely in your application.

```csharp
public sealed class Rfc6238TotpGenerator
{
    // Defaults (6 digits, 30-second steps) match mainstream authenticator apps; digits must be 6–8.
    Rfc6238TotpGenerator(int digits = 6, int stepSeconds = 30);
    // A fresh 20-byte secret: RFC 4226 §4 requires ≥128 bits and recommends 160, an HMAC-SHA1 output.
    static byte[] GenerateSecret();
    // The otpauth:// URI an authenticator app scans, carrying this instance's digits and period.
    string BuildProvisioningUri(string issuer, string accountName, ReadOnlySpan<byte> secret);
    long GetTimeStep(DateTimeOffset timestamp);
    string Generate(ReadOnlySpan<byte> secret, long timeStep);
    // Reports the matched step so you can persist it and reject replay.
    bool TryValidate(ReadOnlySpan<byte> secret, string code, long currentStep, int window, out long matchedStep);
}

public static class Base32   // RFC 4648, encodes without padding
{
    static string Encode(ReadOnlySpan<byte> data);
    // Ignores whitespace, tolerates trailing '=', case-insensitive; throws on a bad character or count.
    static byte[] Decode(string input);
}

public interface ISecretCipher   // AesGcmSecretCipher is the AES-256-GCM implementation
{
    byte EnvelopeVersion { get; }                   // leading envelope byte; increases when the layout changes
    byte[] Encrypt(ReadOnlySpan<byte> plaintext);   // self-describing envelope: [version][nonce][tag][ciphertext]
    byte[] Decrypt(ReadOnlySpan<byte> envelope);    // throws on tamper / wrong key
}
```

`BuildProvisioningUri` emits the digit count and time-step **of the instance you call it on**, so the URI an authenticator app provisions from cannot disagree with the codes that same instance validates. A hand-built URI can, and the mismatch surfaces only as codes that never match. The third thing an app reads from the URI, `algorithm`, is written as a fixed `SHA1` — correct because the generator is HMAC-SHA1 and offers no choice of anything else. It rejects an empty or whitespace issuer or account name, an empty secret, and a `:` in either string — the colon separates the two halves of the URI's label, so one inside a component silently corrupts how the app splits it.

`Base32` is a generic RFC 4648 codec. TOTP enrollment uses `Encode` for both halves of what you show the user — the `secret=` parameter of the URI and the secret they type in by hand — and `Decode` for the reverse direction: taking a secret a user pastes back in, from an existing enrollment being imported or a recovery flow.

`Decode` is lenient about **presentation**, because a human types or pastes the input: whitespace is ignored wherever it appears (so the space-grouped layout authenticator apps display, `MZXW 6YTB OI`, decodes as shown), trailing `=` padding is tolerated, and the alphabet is matched case-insensitively. Input carrying no base32 characters at all — empty, only whitespace, only padding — decodes to an empty array. It is strict about the **characters themselves**, which is the opposite concern: a character outside the alphabet, and a character count no encoding could have produced (one character too few or too many), both raise `FormatException` rather than being skipped or truncated. A mistyped secret has to fail at the decode, not decode into a different secret that fails later as an unexplained wrong code.

Register the cipher with a 32-byte key kept **outside** your datastore (e.g. an environment variable), so a database or backup dump alone can't recover the secret:

```csharp
byte[] key = Convert.FromBase64String(Environment.GetEnvironmentVariable("MFA_SECRET_ENCRYPTION_KEY")!);
builder.Services.AddSingleton<ISecretCipher>(new AesGcmSecretCipher(key));
builder.Services.AddSingleton<Rfc6238TotpGenerator>();
```

**Enrollment** — generate a secret, store the ciphertext, show the user a QR code and the typable secret:
```csharp
string provisioningUri;
string manualEntrySecret;

byte[] secret = Rfc6238TotpGenerator.GenerateSecret();
try
{
    // Persisting EnvelopeVersion alongside the ciphertext lets you find rows on an older format to re-encrypt.
    await _store.SaveEnrollment(userId, _cipher.Encrypt(secret), _cipher.EnvelopeVersion);

    provisioningUri = _totp.BuildProvisioningUri("Acme Corp", user.Email, secret); // render as a QR code
    manualEntrySecret = Base32.Encode(secret);                                     // for typing in by hand
}
finally
{
    // Don't leave the plaintext secret lingering in a managed buffer longer than needed.
    CryptographicOperations.ZeroMemory(secret);
}
```

**Verification** — decrypt, validate within a ±1-step skew window, then advance the replay high-water-mark:
```csharp
byte[] secret = _cipher.Decrypt(await _store.GetCiphertext(userId));
try
{
    long currentStep = _totp.GetTimeStep(DateTimeOffset.UtcNow);
    if (_totp.TryValidate(secret, presentedCode, currentStep, window: 1, out long matchedStep)
        // RFC 6238 §5.2: atomically require matchedStep > the stored last-used step, then store it. This
        // rejects any code at or below the last accepted step, including a still-in-window neighbour.
        && await _store.TryAdvanceLastUsedStep(userId, matchedStep))
    {
        // code accepted
    }
}
finally
{
    CryptographicOperations.ZeroMemory(secret);
}
```

**Replay protection is yours to enforce — and it is the part that gets written wrong.** The library deliberately ships no coordinator for it, because the step it has to claim lives in your datastore. The shape above is the whole pattern, and every clause of it matters:

- **`TryValidate` alone is not acceptance.** It answers whether the code matches *some* step in the skew window; a code stays valid for the whole window, so the same one replays successfully until it falls out. Acceptance is `TryValidate` **and** winning the step.
- **Claim the step atomically, in the store.** `TryAdvanceLastUsedStep` should be a single conditional write — `UPDATE … SET last_used_step = @matchedStep WHERE user_id = @userId AND (last_used_step IS NULL OR last_used_step < @matchedStep)` — accepting only if it updated a row. Read-then-write in application code leaves a window where two concurrent requests both read the old value and both accept the same code.
- **Advance to a high-water-mark, not a used-code set.** Requiring `matchedStep` to be strictly greater than the stored step rejects a still-in-window *neighbouring* step too, which per-code consumption alone would leave open.
- **The same call is what confirms enrollment.** Validating the confirmation code through this path claims its step, so the code the user just typed cannot be replayed at the next step-up moments later.
- **Zero the plaintext secret in a `finally`.** `CryptographicOperations.ZeroMemory` on every path out, including the rejection paths. It reaches the `byte[]` and nothing else: the provisioning URI and the manual-entry string are immutable managed strings holding the same secret, and .NET gives you no way to erase them — they live until they are collected. That is unavoidable, so treat it as the reason to build them once, at enrollment, and never to log or cache either one.

**Key Features:**
- RFC 6238 TOTP over RFC 4226 HOTP with HMAC-SHA1 (what mainstream authenticator apps implement), configurable digit count and time-step
- Enrollment covered end to end: a correctly-sized random secret, the `otpauth://` provisioning URI, and the base32 encoding both the URI and manual entry need
- The provisioning URI advertises the instance's own digits and period, so it cannot drift out of agreement with what that instance validates
- Constant-time code comparison; validation reports the matched step so the caller can reject any code at or below the last accepted step (RFC 6238 §5.2 high-water-mark)
- AES-256-GCM authenticated encryption — a tampered or wrong-key ciphertext fails closed rather than returning garbage
- Self-describing ciphertext envelope with a leading, integrity-protected version byte for future format evolution
- Stateless: the application owns the clock, the secret store, and the atomic replay high-water-mark

## Advanced Configuration

### Service Registration Options

**Service Registration:**
- Register `IAuthRequestValidationService` using standard DI. The library resolves it from DI per-request when auth endpoints are called.
  ```csharp
  builder.Services.AddScoped<IAuthRequestValidationService, MyAuthService>();
  ```
- **Service Lifetime**: Use `AddScoped` when your validation service has database dependencies (e.g., Entity Framework DbContext). Use `AddSingleton` only if the service is stateless and thread-safe.

### Full Configuration Example

Both `AddEasyReasyAuth` and `UseEasyReasyAuth` accept an optional configuration action. All options have sensible defaults, so you only set what you need:

```csharp
builder.Services.AddEasyReasyAuth(jwtSecret, options =>
{
    options.Issuer = "my-issuer";                        // null = issuer validation disabled (default)
    options.Audience = "my-api";                         // null = audience validation disabled (default)
    options.ClockSkew = TimeSpan.FromSeconds(30);        // default; Microsoft default is 5 minutes
    options.RegisterJwtTokenService = true;              // default; set false to register your own
});

app.UseEasyReasyAuth(options =>
{
    options.Enabled = true;                              // default; set false to disable progressive delay
    options.TrustedProxyCount = 2;                       // 0 = ignore X-Forwarded-For (default)
    options.FreeFailures = 10;                           // failures before delays start (default)
    options.DelayIncrement = TimeSpan.FromMilliseconds(500); // delay per failure above threshold (default)
    options.MaxDelay = TimeSpan.FromSeconds(30);         // maximum delay cap (default)
    options.FailureEntryLifetime = TimeSpan.FromHours(1); // stale entry eviction (default)
});
```

### Opting Out of Automatic Service Registration

If you need more control over the `IJwtTokenService` registration (e.g., for testing, custom implementations, or multiple configurations), you can opt out of automatic registration:

```csharp
builder.Services.AddEasyReasyAuth(jwtSecret, options =>
{
    options.Issuer = "my-issuer";
    options.RegisterJwtTokenService = false;
});

// Manually register your own implementation
builder.Services.AddSingleton<IJwtTokenService>(new MyCustomJwtTokenService(jwtSecret, issuer));
```

This is useful for:
- **Testing scenarios**: Mock the service in unit tests
- **Custom implementations**: Use your own `IJwtTokenService` implementation
- **Multiple configurations**: Register different JWT services for different purposes
- **Performance optimization**: Control the service lifetime (singleton vs scoped vs transient)

## Progressive Delay Middleware

The progressive delay middleware helps protect your API from brute-force attacks by introducing a delay for repeated unauthorized requests from the same IP address.

- **How it works:**
  - The first N failed (401 Unauthorized) requests from an IP have no delay (default: 10, configurable via `FreeFailures`).
  - After that, each additional failed request adds an incremental delay (default: 500ms, configurable via `DelayIncrement`), up to a configurable maximum (default: 30 seconds, configurable via `MaxDelay`).
  - The delay is applied before the response is sent, so the attacker must wait.
  - The count is cleared only by a request that actually authenticated the caller — the built-in credential and refresh endpoints report this by calling `HttpContext.MarkAuthenticationSucceeded()` when they issue a token. Every other outcome leaves the count as it stands, so a request that cannot authenticate anyone (a 404, a credential-less 400, or the anonymous logout endpoint's 204) cannot be used to reset the delay between guesses. An endpoint of your own that issues tokens should call the same method.
  - Stale failure entries are automatically evicted after `FailureEntryLifetime` (default: 1 hour).
- **Enabled by default:**
  - The middleware is included automatically when you call `app.UseEasyReasyAuth()`.
- **How to disable:**
  ```csharp
  app.UseEasyReasyAuth(options =>
  {
      options.Enabled = false;
  });
  ```
- **Reverse proxy support:**
  - By default, the middleware uses the direct connection IP (`RemoteIpAddress`) and ignores `X-Forwarded-For` — this prevents IP spoofing attacks.
  - If your app is behind reverse proxies, set `TrustedProxyCount` to the number of proxies in front of your app:
    ```csharp
    app.UseEasyReasyAuth(options =>
    {
        options.TrustedProxyCount = 2; // Behind two nginx proxies
    });
    ```

## Core Features

- **JWT token service**: Issue tokens with custom claims, roles, optional issuer, and optional audience (`aud` claim)
- **Token security**: Each token includes `jti` (unique ID for revocation support) and `nbf` (not-before) claims, with a configurable clock skew (default 30 seconds)
- **Audience validation**: Opt-in `aud` claim prevents tokens issued for one service from being accepted by another
- **Automatic auth endpoints**: Create API key and username/password authentication endpoints with minimal code
- **Cache-Control headers**: Auth endpoints automatically set `Cache-Control: no-store` to prevent token caching
- **Flexible validation**: Implement `IAuthRequestValidationService` to handle any authentication logic (database, external APIs, etc.)
- **Refresh token rotation**: Opt-in refresh tokens with token family tracking and automatic theft detection
- **Secure password hashing**: PBKDF2 with HMAC-SHA512, max password length enforcement, and constant-time comparison
- **Password reset tokens**: Cryptographically secure token generation with SHA-256 hashing for storage
- **MFA primitives**: RFC 6238 TOTP generator (secret generation, `otpauth://` provisioning URI, code validation), RFC 4648 base32 codec, and AES-256-GCM secret cipher — storage-agnostic building blocks for time-based one-time-password and encrypt-at-rest flows
- **Claims injection middleware**: Makes user/tenant IDs available in `HttpContext.Items`
- **Role access**: Retrieve all roles for the current user via `GetRoles()`
- **Claim access**: Retrieve any claim value by key or enum via `GetClaimValue()`
- **Progressive delay middleware**: Configurable brute-force protection with reverse proxy support and automatic stale entry eviction
- **Options pattern configuration**: `Action<T>` lambdas for both `AddEasyReasyAuth` and `UseEasyReasyAuth` — simple defaults, opt-in customization
- **Secret redaction**: `ToString()` on request/response models redacts secrets; `FromJson` exceptions never leak raw input
- **Secret length enforcement**: Secret must be at least 32 bytes (256 bits) for HS256
- **Async support**: All validation methods are async for database lookups and external API calls

## Error Handling

- If the JWT secret is too short, `JwtTokenService` throws an `ArgumentException` with a clear message.
- Invalid options (negative clock skew, negative delay values, etc.) throw `ArgumentOutOfRangeException` at startup.
- `FromJson` methods on request/response models throw sanitized exceptions that never include the raw JSON input.
- Progressive delay is enabled by default; disable it via the options pattern if needed.

## Best Practices

1. **Use a strong, unique secret**: At least 32 bytes (256 bits)
2. **Set issuer and audience**: Prevents cross-service token misuse
3. **Enable progressive delay**: Protects against brute-force attacks by default
4. **Set `TrustedProxyCount`**: If behind reverse proxies, so the middleware sees real client IPs
5. **Access claims and roles via extension methods**: Use `GetUserId()`, `GetTenantId()`, `GetRoles()`, and `GetClaimValue()` for convenience
6. **Implement atomic `MarkAsConsumedAsync`**: If using refresh tokens, ensure your store prevents concurrent redemption

---

For more details, see XML comments in the code or explore the source. This library is designed to be easy to use and secure enough for most uses cases by default.

## Migration from 5.5.0

Version 5.6.0 is additive: three pieces of pure, spec-defined TOTP support code that every consumer of the 5.4.0 MFA primitives had to write before those primitives were usable. No behaviour changed, nothing was removed or resigned, and no endpoint or wire format is touched. The one thing to watch is a name collision, below.

### New: `Rfc6238TotpGenerator.GenerateSecret()`
- **A static method returning a fresh 20-byte secret**, replacing the `RandomNumberGenerator.GetBytes(20)` call and its justifying comment that each consumer wrote for itself. RFC 4226 §4 requires at least 128 bits and recommends 160 — the output length of HMAC-SHA1, and what authenticator apps assume.
- **The size is fixed, not a parameter.** Unlike the digit count and the time-step, the secret's length is never consulted by validation, so it is not instance configuration — and a knob here could only be turned toward a weaker value.

### New: `Rfc6238TotpGenerator.BuildProvisioningUri(issuer, accountName, secret)`
- **An instance method building the `otpauth://totp/…` Key URI** an authenticator app scans, base32-encoding the secret and URI-escaping the `issuer:accountName` label and the `issuer` parameter.
- **It emits the instance's own digits and period.** A hand-built URI typically hardcodes `algorithm=SHA1&digits=6&period=30`; construct the generator with a different `digits` or `stepSeconds` and that URI keeps advertising the defaults while the validator uses the instance values, with nothing to warn you but codes that never match. Calling this method closes that gap structurally. **If you replace a hand-rolled builder with it, keep passing the same `issuer` string your builder used** — the label and issuer are what an authenticator app displays, so a changed value shows up as a differently-named entry for anyone who re-enrolls.
- **It rejects what would silently corrupt the URI**: `ArgumentNullException` on a null `issuer` or `accountName`, and `ArgumentException` on an empty or whitespace one, on an empty `secret`, and on a `:` inside either string, since the colon is the label separator.

### New: `Base32` — RFC 4648 codec, and the one source-level break to watch
- **`public static class Base32` with `Encode(ReadOnlySpan<byte>)` and `Decode(string)`**, a generic codec with no MFA-specific behaviour. `Encode` covers both halves of what enrollment shows the user — the URI's `secret=` parameter and the hand-typed secret — and `Decode` the reverse direction, a secret pasted back in from an imported enrollment or a recovery flow.
- **`Decode` is lenient about presentation and strict about content, both by contract.** Lenient: whitespace ignored wherever it appears (the space-grouped layout authenticator apps display decodes as shown), trailing `=` padding tolerated, alphabet matched case-insensitively, empty array for input carrying no base32 characters. Strict: `FormatException` on an out-of-alphabet character, and on a character count no encoding could have produced. The two go together — a human types this input, so how it is spaced or cased must not matter, while a dropped character must fail loudly rather than decode into a different secret that surfaces later as an unexplained wrong code.
- **A consumer whose own `Base32` is `using`-imported into a file that also has `using EasyReasy.Auth;` gets `CS0104` (ambiguous reference) on upgrade** — which is the exact shape of a project that wrote one for TOTP and keeps it in a codecs namespace of its own. (A `Base32` declared in the file's *own* namespace keeps winning silently instead, since the enclosing namespace is searched before any `using`.) **The fix is to delete your copy in the same commit as the upgrade**; that is the point of the addition. If you need to keep yours, `using Base32 = YourNamespace.Base32;` in the affected files disambiguates without touching either type.

## Migration from 5.4.0

Version 5.5.0 changes wire behaviour in two places: the two built-in credential endpoints answer a request that carries no credentials with a `400`, and the progressive-delay middleware no longer treats every non-401 response as a reason to forget an IP's accumulated failures. No API was removed or resigned; the one source-level change to watch is the new enum members, below.

### Changed: a credential-less request is a 400, not a 401
- **`POST /api/auth/login`** answers `400 Bad Request` with a validation problem when `username` or `password` is absent or empty, and **`POST /api/auth/apikey`** does the same when `apiKey` is absent or empty. Previously the null bound straight through to `IAuthRequestValidationService`, which looked it up, found nothing, and the endpoint wrote a bare 401 — naming wrong credentials as the cause of a request that never became an authentication attempt.
- **`IAuthRequestValidationService` is not called on that path**, so a validation service no longer receives a request whose non-nullable `Username` / `Password` / `ApiKey` is null at runtime. A consumer that counts failed attempts *inside* its validation service therefore stops counting these requests with no change on its part. **A consumer that counts them in its `IAuthAuditLogger` must exclude the two new failure reasons itself** — the hook still fires, with `Success == false`. See Section 10.
- **The `errors` keys are the wire field names** (`username`, `password`, `apiKey`) — the names the endpoint publishes, now pinned on the request models with `[JsonPropertyName]` so they cannot drift from a property rename. `Cache-Control: no-store` is on this 400 as it is on the 401. This describes the 400 the *endpoint* returns: a body that fails to bind at all (empty, unparseable, or a bare `null`) is rejected by the framework before the endpoint runs, so it carries neither the header nor an audit row.
- **Only the emptiness of the field changed meaning.** A whitespace-only value is a value the caller supplied: it still goes on to the validation service and takes the ordinary 401. An unknown user, a wrong password and a locked account all keep the same reasonless 401, indistinguishable from each other.
- **The request models' nullability is unchanged.** `Username`, `Password` and `ApiKey` stay non-nullable, and the endpoint refusing the null is what makes those declarations true downstream. (The models did gain the wire-name constants below.)

### New: the wire field names as constants
- **`LoginAuthRequest.UsernameFieldName` / `PasswordFieldName` and `ApiKeyAuthRequest.ApiKeyFieldName` / `ClientIdFieldName`** expose the JSON names the endpoints key their `errors` object by, so a consumer matching on those keys can reference the constant instead of a string literal. The same constants exist on the `EasyReasy.Auth.Client` copies of both models.

### New: a failure reason for the credential-less request
- **`LoginFailureReason.MissingCredentials`** and **`ApiKeyAuthFailureReason.MissingKey`** flow to `IAuthAuditLogger.OnLoginAsync` / `OnApiKeyAuthAsync` on the 400 path, which still fires exactly once so the audit trail keeps a row for the attempt. The result carries the attempted identifier when the request supplied one (a login missing only its password reports the username) and `null` when the identifier is what was missing.
- **A consumer mapping these enums exhaustively needs a new arm.** A `switch` *statement* with a `default`, or a `switch` *expression* with a `_` arm, keeps compiling untouched. A `switch` expression without one now warns `CS8509` — which is an error under `TreatWarningsAsErrors`. Both members are appended after `Other`, so the numeric value of every existing member is unchanged.

### Changed: only a successful authentication clears the progressive-delay failure count
- **`ProgressiveDelayMiddleware` now clears an IP's accumulated failure count only for a request that authenticated the caller.** It previously cleared on any response that was not a 401 — which let an attacker reset the delay between guesses with a request that is free to make and authenticates nobody: a 404, a credential-less 400, or a call to the anonymous logout endpoint this library maps by default, which always answers 204. A 401 still increments the count; every other outcome leaves it as it stands.
- **New `HttpContext.MarkAuthenticationSucceeded()`** is how an endpoint reports that it issued a token. The built-in credential and refresh endpoints call it themselves. **If you map your own token-issuing endpoint, call it there too**, or a successful login through that endpoint will no longer clear the count. Reading the flag back is `HttpContext.HasAuthenticationSucceeded()`.
- **Nothing to configure.** `ProgressiveDelayOptions` is unchanged. Legitimate traffic that interleaves other responses between logins will see the delay persist where it used to reset — the intended behaviour, since only a successful authentication proves the caller is not guessing.

### New: the 400 is in the OpenAPI document
- **The two credential endpoints declare their `200`, `400` and `401` responses**, so consumers generating a client from the spec see the validation problem. The refresh and logout endpoints are unannotated as before.

## Migration from 5.1.0

Version 5.2.0 is additive. Existing callers using the built-in `RefreshTokenService` compile and behave identically. The new capability is targeted retirement of a single prior refresh-token family at token re-issue. See Section 9 "Targeted session supersession on re-issue".

### New: name and retire a prior family at re-issue
- **`IRefreshTokenService.CreateRefreshTokenWithFamilyAsync`** returns a `RefreshTokenCreationResult` carrying both the `RawToken` and the generated `FamilyId`. The existing `CreateRefreshTokenAsync` is unchanged — it now delegates to this single shared code path and returns `result.RawToken`.
- **`EasyReasyClaim.RefreshFamilyId` / `HttpContext.GetRefreshFamilyId()`** read the `family_id` claim. The refresh path injects this claim authoritatively from the server-side family id on every refresh, so it is stable across rotations and cannot be spoofed via stored claims.
- **`IRefreshTokenService.RetireFamilyAsync(familyId, subject?, httpContext?, ct)`** retires exactly one family (other devices stay live). Null/whitespace family id is a no-op. When a non-empty family id is retired and an audit logger is registered, it fires the new hook.
- **New `IAuthAuditLogger.OnSessionSupersededAsync(httpContext, FamilyRetirementResult)` hook** reports the supersession. It has a default no-op implementation, so existing audit loggers are unaffected. Distinct from `OnLogoutAsync` (a real logout) and `OnConcurrentSessionsRevokedAsync` (global single-session enforcement).

**Rollout is safe with no consumer action.** Access tokens minted before 5.2.0 carry no `family_id`, so `GetRefreshFamilyId()` returns `null` and `RetireFamilyAsync(null)` is a clean no-op. Existing sessions self-heal — each gains the `family_id` claim on its next refresh. A consumer that wants the claim on the very first (login) access token adds it itself using the `FamilyId` from `CreateRefreshTokenWithFamilyAsync`.

**`IRefreshTokenService` gains two methods; nothing else changes.** No `IRefreshTokenStore`, DI-registration, or wire-format changes, and the new `IAuthAuditLogger` member is a default-method addition so existing audit loggers keep compiling. The two new `IRefreshTokenService` methods are *not* default-implemented, so a **custom `IRefreshTokenService` implementation** (rare — most consumers implement only `IRefreshTokenStore` and use the built-in service) must add `CreateRefreshTokenWithFamilyAsync` and `RetireFamilyAsync`.

## Migration from 5.0.0

Version 5.1.0 is additive — no breaking changes. Existing callers compile and behave identically. The new capability is opt-in single-session enforcement.

### New: `ConcurrentSessionPolicy`
- **New optional `concurrentSessionPolicy` parameter** on `AddRefreshTokenService<TStore>` (and the `RefreshTokenService` constructor). Defaults to `ConcurrentSessionPolicy.AllowMultiple`, which is identical to 5.0.0 behaviour — no limit on concurrent sessions.
- **`ConcurrentSessionPolicy.SingleSession`** makes each new login revoke the subject's existing sessions so only the newest stays live. See Section 9 "Logout and Bulk Session Revocation → Single-session enforcement" for the contract, its relationship to `InvalidateAllSessionsAsync`, and the concurrency caveat.
- **New `IAuthAuditLogger.OnConcurrentSessionsRevokedAsync` hook** fires when a login revokes one or more prior sessions under `SingleSession`. It has a default no-op implementation, so existing audit loggers are unaffected. It is deliberately distinct from `OnSessionsInvalidatedAsync` (explicit bulk revocation) so the automatic, login-driven revocations can be audited as their own event.
- **No interface or signature changes** to `IRefreshTokenStore`, `IRefreshTokenService`, `RefreshResult`, or the wire format. The new hook is a default-method addition to `IAuthAuditLogger`, so existing implementations keep compiling.

## Migration from 4.0.0

Version 4.1.0 is additive — no breaking changes. Existing callers compile and behave identically. The new capability is one optional DI service: `IRefreshClaimsResolver`, which lets consumers re-evaluate claims and roles on every refresh or deny a refresh outright.

### New: `IRefreshClaimsResolver`
- **Opt-in DI service.** Register an implementation before `AddRefreshTokenService<TStore>` and the refresh path picks it up automatically. Without a registration, refresh behaviour is identical to 4.0.0 — stored claims and roles are replayed verbatim onto the new tokens.
- **Use cases.** Mid-session password expiry enforcement (e.g. 21 CFR Part 11 §11.300), mid-session role demotion, mid-session account disable. See Section 8 "Refresh Tokens → Mid-session re-evaluation" for the full contract and an example implementation.
- **New `RefreshFailureReason.DeniedByResolver`** distinguishes a consumer-driven deny from theft, expiry, or invalidation. It flows through `IAuthAuditLogger.OnRefreshAsync` like every other refresh failure.
- **New `RefreshFailureReason.ResolverError`** is emitted to `IAuthAuditLogger.OnRefreshAsync` when a resolver throws, so the audit trail records every refresh outcome including faults (ISO 27001 A.12.4.1). The original exception still propagates out of `RefreshAsync` with its original stack trace, so consumer exception-handling middleware is unaffected.
- **No interface or signature changes.** `IRefreshTokenService`, `IRefreshTokenStore`, `RefreshResult`, and the wire format are all unchanged.

## Migration from 3.x

Version 4.0.0 introduces breaking changes. All are in service of structured results that flow into `IAuthAuditLogger` — the new optional DI service that backs ISO 27001–style security audit logging.

### Validation Service
- **`IAuthRequestValidationService` return types changed**:
  - `ValidateLoginRequestAsync` now returns `Task<LoginResult>` (was `Task<AuthResponse?>`).
  - `ValidateApiKeyRequestAsync` now returns `Task<ApiKeyAuthResult>` (was `Task<AuthResponse?>`).
- **Both result types carry an `AttemptedSubject` / `AttemptedClientId`** that must be populated even on failure when knowable (e.g. `UnknownUser`, `UnknownKey`) so audit logs can attribute failed authentication attempts to an identifier. See Section 10 "Security Audit Logging".
- **Neither result type carries the raw credential** (password / API key). Consumers logging the result cannot accidentally leak secrets.

Migration shape:
```csharp
// Before
return valid ? new AuthResponse(...) : null;

// After
return valid
    ? LoginResult.Succeeded(new AuthResponse(...), user.Id)
    : LoginResult.Failed(LoginFailureReason.InvalidCredentials, attemptedSubject: user.Id);
```

### Refresh Token Store
- **`IRefreshTokenStore.InvalidateAllFamiliesForUserAsync`** is a new required method and returns `Task<int>` (count of families invalidated), flowing into `SessionRevocationResult.InvalidatedFamilyCount`. Typical SQL: `UPDATE refresh_tokens SET invalidated = true WHERE subject = @subject AND invalidated = false` — return the distinct-family count.
- **All `IRefreshTokenStore` methods now take `CancellationToken cancellationToken = default`**: `StoreAsync`, `GetByTokenHashAsync`, `MarkAsConsumedAsync`, `InvalidateFamilyAsync`. Callers relying on the default value require no changes; custom implementations must add the parameter.

### Refresh Token Service
- **`IRefreshTokenService.LogoutAsync` and `IRefreshTokenService.InvalidateAllSessionsAsync` are new additions in 4.0.0** (not renames of pre-3.x methods). Custom implementations of `IRefreshTokenService` must add both — the built-in `RefreshTokenService` already implements them.
- **`RefreshAsync` signature changed**: `Task<RefreshResult> RefreshAsync(string refreshToken, IJwtTokenService jwtTokenService, HttpContext? httpContext = null, CancellationToken cancellationToken = default)`. The new `httpContext` parameter flows into `IAuthAuditLogger.OnRefreshAsync`; pass `null` for programmatic refreshes. Existing positional callers of just `(refreshToken, jwtTokenService)` compile unchanged.
- **`LogoutAsync` signature**: `Task<LogoutResult> LogoutAsync(string? refreshToken, HttpContext? httpContext = null, CancellationToken cancellationToken = default)`. The built-in endpoint still returns `204` on the wire; the structured result flows into `IAuthAuditLogger.OnLogoutAsync`. `refreshToken` is nullable — null/empty is a no-op. `httpContext` is null when called programmatically.
- **`InvalidateAllSessionsAsync` signature**: `Task<SessionRevocationResult> InvalidateAllSessionsAsync(string subject, CancellationToken cancellationToken = default)`. Returns the count of families invalidated, fed into `IAuthAuditLogger.OnSessionsInvalidatedAsync`.
- **`CreateRefreshTokenAsync` now takes `CancellationToken cancellationToken = default`**. Existing callers are unaffected; custom implementations must add the parameter.

### Auth Endpoints
- **`AddAuthEndpoints` signature extended**: new `bool allowLogout = true` parameter controls the `POST /api/auth/logout` endpoint. **Breaking for existing callers who did not register `IRefreshTokenService`**: because the default enables logout, `AddAuthEndpoints` will throw at startup unless you either register a refresh token service (`builder.Services.AddRefreshTokenService<MyStore>()`) or explicitly opt out with `allowLogout: false`.
- **`AddAuthEndpoints` startup validation is now conditional**: the check for `IAuthRequestValidationService` only fires when `allowApiKeys || allowUsernamePassword`; the check for `IRefreshTokenService` only fires when `allowRefresh || allowLogout`. A caller enabling only one endpoint family no longer needs to register services for the other.
- **Refresh and logout endpoints now honour `HttpContext.RequestAborted`**: a client disconnect cancels the in-flight token operation.
- **Audit hooks are split between the endpoint layer and the service layer**: `OnLoginAsync` and `OnApiKeyAuthAsync` are invoked by the endpoint (because `IAuthRequestValidationService` is consumer-implemented and cannot be guaranteed to call the hook). `OnRefreshAsync`, `OnLogoutAsync`, and `OnSessionsInvalidatedAsync` are invoked inside `RefreshTokenService`, so both HTTP and programmatic callers trigger them uniformly. See `IAuthAuditLogger` remarks.

### New: `IAuthAuditLogger`
- Opt-in DI service with default no-op methods — implement only what you need. See Section 10 "Security Audit Logging" for the full hook list and an example implementation. Registering one does not change wire behaviour; it only enables audit records.
- **Register as singleton or scoped.** `RefreshTokenService` captures the logger at construction time, and the library registers the service as scoped — so both singleton and scoped loggers work correctly. Transient is wasteful but harmless. The only broken shape is a logger with a shorter lifetime than `IRefreshTokenService` itself, which in practice can only happen if you re-register the service as singleton while keeping a scoped logger.

## Migration from 2.x

Version 3.0.0 introduces breaking changes:

### Password Hashing
- **`IPasswordHasher` signature changed**: The `username` parameter has been removed from both `HashPassword` and `ValidatePassword`. Password hashing now uses only the password with a cryptographic random salt.
- **V2/V3 hashes are no longer verifiable**: The new V4 hash format is the only supported format. Existing password hashes cannot be verified with this version.
- **Migration path**: Use the new `IPasswordResetTokenHandler` to implement a password reset flow. Existing users with old hashes will need to reset their passwords through this mechanism.

### Configuration API
- **`AddEasyReasyAuth` signature changed**: The `issuer`, `registerJwtTokenService`, and `clockSkew` parameters have been replaced by an `Action<EasyReasyAuthOptions>` lambda. Update calls like `AddEasyReasyAuth(secret, issuer: "x")` to `AddEasyReasyAuth(secret, o => { o.Issuer = "x"; })`.
- **`UseEasyReasyAuth` signature changed**: The `enableProgressiveDelay` and `trustedProxyCount` parameters have been replaced by an `Action<ProgressiveDelayOptions>` lambda. Update calls like `UseEasyReasyAuth(trustedProxyCount: 2)` to `UseEasyReasyAuth(o => { o.TrustedProxyCount = 2; })`.

### Behavioral Changes
- **Clock skew reduced**: Default clock skew is now 30 seconds (was 5 minutes). Tokens that expired within the last 5 minutes may now be rejected. Increase `ClockSkew` in options if this causes issues.
- **`MarkAsConsumedAsync` return type**: Changed from `Task` to `Task<bool>` to support atomic consumption. Update your `IRefreshTokenStore` implementations accordingly.

---

⚠️ **Security Disclaimer:** This library implements a simple token-based authentication system suitable for internal or low-risk applications. It does not follow full OAuth2/OIDC standards and lacks advanced features like key rotation, token introspection, consent management, and third-party identity federation. For production systems with complex threat models, consider using a mature identity provider such as IdentityServer, OpenIddict, or a cloud-based solution. 