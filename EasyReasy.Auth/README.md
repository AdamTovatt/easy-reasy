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

var app = builder.Build();
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

    public async Task<AuthResponse?> ValidateApiKeyRequestAsync(ApiKeyAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
    {
        // Validate API key (e.g., check database, external service, etc.)
        var user = await _userRepository.GetByApiKeyAsync(request.ApiKey);
        if (user == null) return null;

        // Extract tenant ID from header if available
        string? tenantId = user.TenantId;
        if (httpContext?.Request.Headers.TryGetValue("X-Tenant-ID", out var headerTenantId) == true)
        {
            tenantId = headerTenantId.ToString();
        }

        // Create JWT token
        DateTime expiresAt = DateTime.UtcNow.AddHours(1);
        string token = jwtTokenService.CreateToken(
            subject: user.Id,
            authType: "apikey",
            additionalClaims: new[] { new Claim("tenant_id", tenantId) },
            roles: user.Roles.ToArray(),
            expiresAt: expiresAt);

        return new AuthResponse(token, expiresAt.ToString("o"));
    }

    public async Task<AuthResponse?> ValidateLoginRequestAsync(LoginAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
    {
        // Validate username/password
        var user = await _userRepository.GetByUsernameAsync(request.Username);
        if (user == null) return null;

        // Validate password
        if (!_passwordHasher.ValidatePassword(request.Password, user.PasswordHash))
            return null;

        // Extract tenant ID from header if available
        string? tenantId = user.TenantId;
        if (httpContext?.Request.Headers.TryGetValue("X-Tenant-ID", out var headerTenantId) == true)
        {
            tenantId = headerTenantId.ToString();
        }

        // Create JWT token
        DateTime expiresAt = DateTime.UtcNow.AddHours(1);
        string token = jwtTokenService.CreateToken(
            subject: user.Id,
            authType: "user",
            additionalClaims: new[] { new Claim("tenant_id", tenantId) },
            roles: user.Roles.ToArray(),
            expiresAt: expiresAt);

        return new AuthResponse(token, expiresAt.ToString("o"));
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

var app = builder.Build();

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
public async Task<AuthResponse?> ValidateApiKeyRequestAsync(ApiKeyAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
{
    // Validate API key
    var user = await _userRepository.GetByApiKeyAsync(request.ApiKey);
    if (user == null) return null;

    // Extract tenant ID from header
    string? tenantId = null;
    if (httpContext?.Request.Headers.TryGetValue("X-Tenant-ID", out var headerTenantId) == true)
    {
        tenantId = headerTenantId.ToString();
    }

    // Create JWT token with tenant information
    DateTime expiresAt = DateTime.UtcNow.AddHours(1);
    string token = jwtTokenService.CreateToken(
        subject: user.Id,
        authType: "apikey",
        additionalClaims: new[] { new Claim("tenant_id", tenantId) },
        roles: user.Roles.ToArray(),
        expiresAt: expiresAt);

    return new AuthResponse(token, expiresAt.ToString("o"));
}
```

**Example: Accessing Query Parameters**
```csharp
public async Task<AuthResponse?> ValidateLoginRequestAsync(LoginAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
{
    // Validate credentials
    var user = await _userRepository.GetByUsernameAsync(request.Username);
    if (user == null || !VerifyPassword(request.Password, user.PasswordHash)) 
        return null;

    // Extract organization from query parameter
    string? organization = httpContext?.Request.Query["org"].ToString();

    // Create JWT token with organization information
    DateTime expiresAt = DateTime.UtcNow.AddHours(1);
    string token = jwtTokenService.CreateToken(
        subject: user.Id,
        authType: "user",
        additionalClaims: new[] 
        { 
            new Claim("tenant_id", user.TenantId),
            new Claim("organization", organization)
        },
        roles: user.Roles.ToArray(),
        expiresAt: expiresAt);

    return new AuthResponse(token, expiresAt.ToString("o"));
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
    public Task StoreAsync(StoredRefreshToken refreshToken) { /* INSERT into DB */ }
    public Task<StoredRefreshToken?> GetByTokenHashAsync(string tokenHash) { /* SELECT by hash */ }
    public Task<bool> MarkAsConsumedAsync(string tokenHash, DateTime consumedAt) { /* see note below */ }
    public Task InvalidateFamilyAsync(string familyId) { /* UPDATE SET invalidated WHERE family_id = ... */ }
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

3. **Issue refresh tokens in your validation service** by injecting `IRefreshTokenService`:
```csharp
public class MyAuthService : IAuthRequestValidationService
{
    private readonly IRefreshTokenService _refreshTokenService;

    public MyAuthService(IRefreshTokenService refreshTokenService)
    {
        _refreshTokenService = refreshTokenService;
    }

    public async Task<AuthResponse?> ValidateLoginRequestAsync(
        LoginAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
    {
        // ... validate credentials, create access token ...

        string refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(
            subject: user.Id,
            authType: "user",
            serializedClaims: RefreshTokenService.SerializeClaims(claims),
            serializedRoles: RefreshTokenService.SerializeRoles(roles));

        return new AuthResponse(token, expiresAt.ToString("o"), refreshToken);
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
public async Task<bool> MarkAsConsumedAsync(string tokenHash, DateTime consumedAt)
{
    // Only updates if consumed_at is still NULL — returns true if 1 row was affected
    int affected = await db.ExecuteAsync(
        "UPDATE refresh_tokens SET consumed_at = @consumedAt WHERE token_hash = @tokenHash AND consumed_at IS NULL",
        new { tokenHash, consumedAt });
    return affected == 1;
}
```

For other stores (Redis, MongoDB, etc.), use the equivalent atomic compare-and-set operation. If your store cannot guarantee atomicity, concurrent refresh requests could both succeed, issuing duplicate token pairs.

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