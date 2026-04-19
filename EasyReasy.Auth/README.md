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
- `401 Unauthorized` on invalid credentials

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
    accessTokenLifetime: TimeSpan.FromHours(1));   // default

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
            serializedClaims: RefreshTokenService.SerializeClaims(claims),
            serializedRoles: RefreshTokenService.SerializeRoles(roles));

        return LoginResult.Succeeded(new AuthResponse(token, expiresAt.ToString("o"), refreshToken), user.Id);
    }
}
```

#### How It Works

- Refresh tokens are 32-byte cryptographic random strings, stored as SHA-256 hashes
- Each token belongs to a **family** — when a token is used, a new one is issued in the same family (rotation)
- If a token that was already used gets presented again, the library detects theft and **invalidates the entire family**
- Everything is opt-in: you must register the service, enable the endpoint, and inject `IRefreshTokenService` in your validation service

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

### 10. Security Audit Logging (ISO 27001 A.9 / A.12)

Every authentication event the library surfaces — success or failure — is reported through a single optional DI service: `IAuthAuditLogger`. Register an implementation and the built-in endpoints (plus `RefreshTokenService.InvalidateAllSessionsAsync`) will invoke it with a structured result object.

| Event | Hook | Control | Typical data to log |
|---|---|---|---|
| Successful / failed username+password login | `OnLoginAsync(httpContext, LoginResult)` | ISO 27001 A.12.4.1 | outcome, `AttemptedSubject`, `FailureReason`, IP, User-Agent, time |
| Successful / failed API key auth | `OnApiKeyAuthAsync(httpContext, ApiKeyAuthResult)` | ISO 27001 A.12.4.1 | outcome, `AttemptedClientId`, `FailureReason`, IP, User-Agent, time |
| Refresh (incl. `TheftDetected`) | `OnRefreshAsync(httpContext, RefreshResult)` | ISO 27001 A.12.4.1 | outcome, `Subject`, `FamilyId`, `FailureReason`, IP, time |
| Logout | `OnLogoutAsync(httpContext, LogoutResult)` | ISO 27001 A.9.2.6 | `WasKnown`, `Subject`, `FamilyId`, IP, time |
| Bulk session revocation | `OnSessionsInvalidatedAsync(SessionRevocationResult)` | ISO 27001 A.9.2.6 | `Subject`, `InvalidatedFamilyCount`, time |

All methods have default no-op implementations — implement only the events you care about. The result objects deliberately never carry a raw password or a raw API key, so failure records are safe to serialise to your log store.

⚠️ **Do not serialise the whole result object on the success path.** `LoginResult` and `ApiKeyAuthResult` embed an `AuthResponse` that carries the issued JWT access token and (when refresh tokens are enabled) the raw refresh token — both are bearer credentials. Log **metadata** from the result (`Success`, `AttemptedSubject` / `AttemptedClientId`, `FailureReason`) — never log `result.AuthResponse` with a structured logger's destructuring syntax (e.g. Serilog `{@result}`) or `JsonSerializer.Serialize(result)`. The example below follows the right pattern.

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
        // TheftDetected is particularly worth alerting on.
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
  - The delay is reset after a successful (non-401) request.
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