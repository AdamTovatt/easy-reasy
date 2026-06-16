# EasyReasy.Auth.Google

Google Sign-In integration for the EasyReasy.Auth JWT authentication pipeline. Validates Google ID tokens and lets your application issue the same JWTs (and optional refresh tokens) as any other auth method.

## Quick Start

### 1. Install

```bash
dotnet add package EasyReasy.Auth.Google
```

### 2. Implement `IGoogleAuthHandler`

This is where you decide what happens after a Google user is verified - look them up, create them, assign claims and roles, and build the JWT:

```csharp
public class MyGoogleAuthHandler : IGoogleAuthHandler
{
    private readonly IUserRepository _users;

    public MyGoogleAuthHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<AuthResponse?> HandleGoogleUserAsync(
        GoogleUserInfo userInfo,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService? refreshTokenService,
        HttpContext? httpContext)
    {
        User user = await _users.FindOrCreateByGoogleSubjectAsync(
            userInfo.Subject, userInfo.Email, userInfo.Name);

        DateTime expiresAt = DateTime.UtcNow.AddHours(1);
        string token = jwtTokenService.CreateToken(
            subject: user.Id.ToString(),
            authType: "user",
            additionalClaims: [],
            roles: user.Roles,
            expiresAt: expiresAt);

        string? refreshToken = null;
        if (refreshTokenService != null)
        {
            refreshToken = await refreshTokenService.CreateRefreshTokenAsync(
                user.Id.ToString(), "user", null, null);
        }

        return new AuthResponse(token, expiresAt.ToString("o"), refreshToken);
    }
}
```

### 3. Register Services

```csharp
// In Program.cs
builder.Services.AddEasyReasyAuth(jwtSecret);
builder.Services.AddEasyReasyGoogleAuth(googleClientId: "your-client-id.apps.googleusercontent.com");
builder.Services.AddScoped<IGoogleAuthHandler, MyGoogleAuthHandler>();

WebApplication app = builder.Build();

app.UseEasyReasyAuth();
app.AddGoogleAuthEndpoint();   // POST /api/auth/google
app.AddAuthEndpoints();        // the built-in EasyReasy.Auth endpoints (login, refresh, logout)
```

## Flow

1. Frontend uses Google's JavaScript SDK - user consents - frontend gets a Google ID token
2. Frontend sends `POST /api/auth/google` with `{ "idToken": "..." }`
3. Library validates the ID token against Google's public keys
4. Library enforces the hosted-domain allowlist and email-verification policy, then extracts `GoogleUserInfo` (subject, email, email-verified, name, picture URL)
5. Library calls your `IGoogleAuthHandler.HandleGoogleUserAsync`
6. You look up or create the user, issue a JWT via `IJwtTokenService`, optionally create a refresh token
7. `AuthResponse` (token, expiration, optional refresh token) is returned to the frontend

## Configuration

### Restrict to Google Workspace Domains

```csharp
builder.Services.AddEasyReasyGoogleAuth(
    googleClientId: "your-client-id.apps.googleusercontent.com",
    allowedHostedDomains: new[] { "yourcompany.com" });
```

When `allowedHostedDomains` is set, only users whose Google account belongs to one of those domains will be accepted. All other tokens are rejected. Domain matching is case-insensitive.

### Email Verification

By default, accounts whose email address is not verified by Google (the token's `email_verified` claim is `false`) are rejected, matching Google's guidance that an unverified email must not be trusted as an identifier. If your application does not rely on the email address for identity, opt out:

```csharp
builder.Services.AddEasyReasyGoogleAuth(
    googleClientId: "your-client-id.apps.googleusercontent.com",
    requireVerifiedEmail: false);
```

Either way, the verification state is exposed on `GoogleUserInfo.EmailVerified` so your `IGoogleAuthHandler` can make its own decision.

## Endpoint Behavior

`AddGoogleAuthEndpoint` maps `POST /api/auth/google` with the same hardening as the built-in EasyReasy.Auth endpoints:

- It is **anonymous**, so it stays reachable even when the application applies a global authorization policy.
- It sets **`Cache-Control: no-store`**, so the issued token is never cached by browsers or proxies.

## Audit Logging

Google sign-in flows into the same audit pipeline as every other EasyReasy.Auth event. If you register an `IAuthAuditLogger` (see the EasyReasy.Auth README, "Security Audit Logging"), the endpoint invokes `OnExternalAuthAsync` after every attempt — success or failure — before the response is written:

```csharp
public class MyAuditLogger : IAuthAuditLogger
{
    private readonly ILogger<MyAuditLogger> _logger;

    public MyAuditLogger(ILogger<MyAuditLogger> logger) { _logger = logger; }

    public Task OnExternalAuthAsync(HttpContext ctx, ExternalAuthResult result)
    {
        _logger.LogInformation(
            "auth.external {Provider} {Outcome} subject={Subject} reason={Reason} ip={Ip}",
            result.Provider,                                  // "google"
            result.Success ? "success" : "failure",
            result.AttemptedSubject,
            result.FailureReason,
            ctx.Connection.RemoteIpAddress);
        return Task.CompletedTask;
    }
}
```

The `ExternalAuthResult.FailureReason` is `InvalidToken` when the Google ID token fails validation (bad signature, expiry, audience mismatch, a disallowed hosted domain, or an unverified email) and `Rejected` when your `IGoogleAuthHandler` returns `null` to decline an otherwise-valid identity. As with the other result types, log the **metadata** shown above — never the whole result, because on success it embeds the issued bearer token.

## Models

### `GoogleUserInfo`

| Property | Type | Description |
|----------|------|-------------|
| `Subject` | `string` (required) | Google's stable unique user ID (the `sub` claim) |
| `Email` | `string` (required) | The user's email address |
| `EmailVerified` | `bool` (required) | Whether Google has verified the user owns this email (the `email_verified` claim) |
| `Name` | `string?` | Display name, or null |
| `PictureUrl` | `string?` | Profile picture URL, or null |

### `GoogleAuthRequest`

| Property | Type | Description |
|----------|------|-------------|
| `IdToken` | `string` | The Google ID token from the frontend |

Follows the standard EasyReasy serialization pattern: `ToJson()`, `FromJson(string)`, `ToString()`.
