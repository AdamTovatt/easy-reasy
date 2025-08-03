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
- **Flexible configuration**: Optional issuer validation, easy integration with ASP.NET Core
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
builder.Services.AddEasyReasyAuth(jwtSecret, issuer: "my-issuer");

var app = builder.Build();
app.UseEasyReasyAuth(); // Progressive delay enabled by default
```

### 3. Issue tokens

#### Option A: Manual Token Creation **(Not recommended, see Option B for recommended way)**
> You probably want to get an instance of IJWtTokenService via dependency injection in your controller class and create an endpoint in that is responsible for issuing tokens if they should be issued.
```csharp
IJwtTokenService tokenService = new JwtTokenService(jwtSecret, issuer: "my-issuer");
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
        // Validate username/password (e.g., check database, hash password, etc.)
        var user = await _userRepository.GetByUsernameAsync(request.Username);
        if (user == null || !VerifyPassword(request.Password, user.PasswordHash)) 
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

Then register the service and add endpoints in `Program.cs`:

```csharp
// Register your validation service
builder.Services.AddAuthValidationService(new MyAuthService());

// ... other configuration ...

var app = builder.Build();

// ... other configuration ...

// Add auth endpoints (choose which ones you want)
app.AddAuthEndpoints(
    app.Services.GetRequiredService<IAuthRequestValidationService>(),
    allowApiKeys: true,
    allowUsernamePassword: true);

// Or add specific endpoints individually:
// app.AddApiAuthEndpoint(app.Services.GetRequiredService<IAuthRequestValidationService>());
// app.AddLoginAuthEndpoint(app.Services.GetRequiredService<IAuthRequestValidationService>());

app.MapControllers(); // This is probably here already
// app.MapControllers(); is not specifically required by the auth library
// it's just to show how the layout would probably look in Program.cs
```

This will automatically create:
- `POST /api/auth/apikey` - For API key authentication
- `POST /api/auth/login` - For username/password authentication

Both endpoints return:
- `200 OK` with `AuthResponse` (token + expiration) on success
- `401 Unauthorized` on invalid credentials

### 4. Accessing HTTP Context in Validation

The `IAuthRequestValidationService` methods now receive an optional `HttpContext` parameter, allowing you to access request headers, query parameters, and other HTTP context information during authentication. This is particularly useful for multi-tenant applications.

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

**Backward Compatibility**: The `HttpContext` parameter is optional and defaults to `null`, so existing implementations will continue to work without modification.

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

### 5. Password Hashing

The library includes a secure password hasher using PBKDF2 with HMAC-SHA512. The `IPasswordHasher` interface provides these methods:

```csharp
public interface IPasswordHasher
{
    // Hash password with username as additional salt
    string HashPassword(string password, string username);
    
    // Validate password with username as additional salt
    bool ValidatePassword(string password, string passwordHash, string username);
}
```

Use it in your `IAuthRequestValidationService` implementation:

```csharp
public class MyAuthService : IAuthRequestValidationService
{
    private readonly IPasswordHasher _passwordHasher;

    public MyAuthService(IPasswordHasher passwordHasher)
    {
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthResponse?> ValidateLoginRequestAsync(LoginAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
    {
        // Get user from database
        var user = await _userRepository.GetByUsernameAsync(request.Username);
        if (user == null) return null;

        // Validate password using username as additional salt
        if (!_passwordHasher.ValidatePassword(request.Password, user.PasswordHash, request.Username))
            return null;

        // Create JWT token...
        DateTime expiresAt = DateTime.UtcNow.AddHours(1);
        string token = jwtTokenService.CreateToken(
            subject: user.Id,
            authType: "user",
            additionalClaims: new[] { new Claim("tenant_id", user.TenantId) },
            roles: user.Roles.ToArray(),
            expiresAt: expiresAt);

        return new AuthResponse(token, expiresAt.ToString("o"));
    }
}
```

Register the password hasher in `Program.cs`:

```csharp
builder.Services.AddSingleton<IPasswordHasher, SecurePasswordHasher>();
```

**Key Features:**
- Uses PBKDF2 with HMAC-SHA512 and 100,000 iterations
- Username as additional salt for extra security
- Versioned hash format for future compatibility
- Constant-time comparison to prevent timing attacks

## Advanced Configuration

### Opting Out of Automatic Service Registration

If you need more control over the `IJwtTokenService` registration (e.g., for testing, custom implementations, or multiple configurations), you can opt out of automatic registration:

```csharp
// Register authentication without automatic IJwtTokenService registration
builder.Services.AddEasyReasyAuth(jwtSecret, issuer: "my-issuer", registerJwtTokenService: false);

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
  - The first 10 failed (401 Unauthorized) requests from an IP have no delay.
  - After that, each additional failed request adds a 500ms delay (e.g., 11th failure = 500ms, 12th = 1000ms, etc.).
  - The delay is reset after a successful (non-401) request.
- **Enabled by default:**
  - The middleware is included automatically when you call `app.UseEasyReasyAuth()`.
- **How to disable:**
  - Pass `enableProgressiveDelay: false` to `UseEasyReasyAuth`:
    ```csharp
    app.UseEasyReasyAuth(enableProgressiveDelay: false);
    ```
- **How to enable:**
  - Omit the parameter or set it to `true` (default):
    ```csharp
    app.UseEasyReasyAuth(); // or app.UseEasyReasyAuth(enableProgressiveDelay: true);
    ```

## Core Features

- **JWT token service**: Issue tokens with custom claims, roles, and optional issuer
- **Automatic auth endpoints**: Create API key and username/password authentication endpoints with minimal code
- **Flexible validation**: Implement `IAuthRequestValidationService` to handle any authentication logic (database, external APIs, etc.)
- **Secure password hashing**: PBKDF2 with HMAC-SHA512, username-based salt, and constant-time comparison
- **Claims injection middleware**: Makes user/tenant IDs available in `HttpContext.Items`
- **Role access**: Retrieve all roles for the current user via `GetRoles()`
- **Claim access**: Retrieve any claim value by key or enum via `GetClaimValue()`
- **Progressive delay middleware**: Slows repeated unauthorized requests from the same IP (first 10 have no delay, then 500ms per failure)
- **Configurable issuer validation**: Pass `issuer: null` to disable
- **Secret length enforcement**: Secret must be at least 32 bytes (256 bits) for HS256
- **Async support**: All validation methods are async for database lookups and external API calls

## Error Handling

- If the JWT secret is too short, `JwtTokenService` throws an `ArgumentException` with a clear message.
- Progressive delay is enabled by default; opt out with `app.UseEasyReasyAuth(enableProgressiveDelay: false);`

## Best Practices

1. **Use a strong, unique secret**: At least 32 bytes (256 bits)
2. **Set issuer for extra validation**: Use the same value when issuing and validating tokens
3. **Enable progressive delay**: Protects against brute-force attacks by default
4. **Access claims and roles via extension methods**: Use `GetUserId()`, `GetTenantId()`, `GetRoles()`, and `GetClaimValue()` for convenience

---

For more details, see XML comments in the code or explore the source. This library is designed to be easy to use and secure enough for most uses cases by default.

---

⚠️ **Security Disclaimer:** This library implements a simple token-based authentication system suitable for internal or low-risk applications. It does not follow full OAuth2/OIDC standards and lacks advanced features like key rotation, token introspection, consent management, and third-party identity federation. For production systems with complex threat models, consider using a mature identity provider such as IdentityServer, OpenIddict, or a cloud-based solution. 