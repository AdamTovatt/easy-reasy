# EasyReasy.Auth.Microsoft

Sign in with Microsoft work and school accounts (Microsoft Entra ID) for the EasyReasy.Auth JWT pipeline. The server runs the authorization code flow with PKCE as a confidential client: the browser only ever carries the authorization code and an encrypted `state`, and the server redeems the code at Microsoft's token endpoint and validates the ID token itself. Your application then issues the same JWTs (and optional refresh tokens) as for any other sign-in.

Beyond signing in, the same service lets your application re-check who someone is at a sensitive moment (for example signing a record): it can require that the person entered their credentials within a given time and passed a second factor through a Conditional Access authentication context.

Personal Microsoft accounts are always refused. The package never creates users: your handler links a verified identity to one of your users by tenant id and object id, never by email.

## Quick Start

### 1. Install

```bash
dotnet add package EasyReasy.Auth.Microsoft
```

### 2. Register the app in Microsoft Entra ID

In the Microsoft Entra admin center, under **App registrations**:

1. **New registration.** For supported account types, choose *Accounts in any organizational directory* to let any work or school tenant sign in (and narrow it with `AllowedTenantIds` or a per-request tenant), or *Accounts in this organizational directory only* for one tenant. Never include personal Microsoft accounts: the package refuses them.
2. **Authentication → Add a platform → Web**, and add each redirect URI your application uses: one page per flow (for example `https://app.example/auth/microsoft/callback` for sign-in and `https://app.example/sign/microsoft/callback` for a re-check at signing). Do not use the *Single-page application* platform: the code is redeemed by the server with a client secret, which only the Web platform allows. Leave the implicit grant options off.
3. **Certificates & secrets → New client secret.** Store the value as a server secret; it is `ClientSecret` below. Certificate credentials are not supported.
4. **Token configuration → Add optional claim → ID**, and add `auth_time` (needed for a max authentication age) and `acrs` (needed for a required authentication context). Without them, a flow that asks for either is refused as `MissingAuthTime` or `MissingAuthenticationContext`.

The scopes are `openid profile`, which need no admin consent.

### 3. Implement `IMicrosoftAuthHandler`

The handler decides the rules a sign-in is begun with, and what happens once a Microsoft identity is verified:

```csharp
public class MyMicrosoftAuthHandler : IMicrosoftAuthHandler
{
    private readonly IUserRepository _users;

    public MyMicrosoftAuthHandler(IUserRepository users)
    {
        _users = users;
    }

    public Task<MicrosoftSignInSettings> GetSignInSettingsAsync(HttpContext httpContext)
    {
        return Task.FromResult(new MicrosoftSignInSettings
        {
            RedirectUri = "https://app.example/auth/microsoft/callback",
        });
    }

    public async Task<AuthResponse?> HandleMicrosoftUserAsync(
        MicrosoftUserInfo userInfo,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService? refreshTokenService,
        HttpContext httpContext)
    {
        // Link by tenant id and object id, never by email.
        User? user = await _users.FindByMicrosoftAccountAsync(userInfo.TenantId, userInfo.ObjectId);
        if (user == null)
        {
            return null; // not linked: the sign-in is refused and audited as Rejected
        }

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
            refreshToken = await refreshTokenService.CreateRefreshTokenAsync(user.Id.ToString(), "user", null, null);
        }

        return new AuthResponse(token, expiresAt.ToString("o"), refreshToken);
    }
}
```

`GetSignInSettingsAsync` is called for every begin request, so the settings can depend on it (for example the tenant of the host the request came to).

### 4. Register services and endpoints

```csharp
// In Program.cs
builder.Services.AddEasyReasyAuth(jwtSecret);
builder.Services.AddEasyReasyMicrosoftAuth(new MicrosoftAuthOptions
{
    ClientId = microsoftClientId,
    ClientSecret = microsoftClientSecret,
    StateKey = microsoftStateKey, // 32 random bytes, used for nothing else
    RedirectUris = ["https://app.example/auth/microsoft/callback"],
});
builder.Services.AddScoped<IMicrosoftAuthHandler, MyMicrosoftAuthHandler>();

WebApplication app = builder.Build();

app.UseEasyReasyAuth();
app.AddMicrosoftAuthEndpoints(); // POST /api/auth/microsoft/begin and POST /api/auth/microsoft
```

The built-in EasyReasy.Auth endpoints are added separately with `app.AddAuthEndpoints(...)`, which needs an `IAuthRequestValidationService` for API key and username/password sign-in and an `IRefreshTokenService` for refresh and logout; see the EasyReasy.Auth README.

Startup fails with one `ArgumentException` listing every problem with the options, and `AddMicrosoftAuthEndpoints` fails at startup when the Microsoft services, your handler or EasyReasy.Auth's `IJwtTokenService` is not registered.

### 5. Call it from the frontend

```javascript
// The sign-in button.
const begin = await fetch("/api/auth/microsoft/begin", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ loginHint: null }), // the body is optional
});
const { authorizationUrl } = await begin.json();
window.location.assign(authorizationUrl);

// The page at the redirect URI.
const query = new URLSearchParams(window.location.search);
if (query.has("error")) {
  // Microsoft refused or the user cancelled; show query.get("error_description").
} else {
  const response = await fetch("/api/auth/microsoft", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ code: query.get("code"), state: query.get("state") }),
  });
  // 200: an AuthResponse, exactly as from the other sign-in endpoints. 401: refused.
}
```

Both calls must reach the API over HTTPS on the same host, with the browser sending cookies: begin sets a `__Host-` binding cookie that the sign-in call must carry back.

## Flow

1. The browser posts to `/api/auth/microsoft/begin`. The endpoint asks your handler for the sign-in's settings, and the service generates a flow id, a PKCE verifier and a nonce, sets the flow's binding cookie, and seals the flow into an encrypted `state`.
2. The browser goes to Microsoft's authorize URL, the user signs in, and Microsoft redirects back to your redirect URI with `code` and `state` in the query (`response_mode=query`, never `form_post`, which a SameSite=Lax cookie would not survive).
3. The page posts `code` and `state` to `/api/auth/microsoft`. The service opens the state, checks and clears the binding cookie, redeems the code at Microsoft's token endpoint with the client secret, the PKCE verifier and the same redirect URI, validates the ID token, and applies the flow's tenant, authentication context and max age rules.
4. Your handler receives the `MicrosoftUserInfo` and returns an `AuthResponse`, or null to refuse.
5. The attempt is reported to `IAuthAuditLogger.OnExternalAuthAsync`, and the response is returned.

## Configuration

`MicrosoftAuthOptions`:

| Property | Required | Description |
|----------|----------|-------------|
| `ClientId` | yes | The app registration's application (client) id. The ID token's audience must equal it. |
| `ClientSecret` | yes | The app registration's client secret. |
| `StateKey` | yes | Exactly 32 bytes, the AES-256 key that seals the `state`. Use a key for this alone, never your MFA-secret cipher's or the JWT secret. Copied at registration. Changing it only fails flows in progress. |
| `RedirectUris` | yes | Every redirect URI a flow may use, at least one; each must also be registered on the app's Web platform. Absolute https URLs (http only on a loopback host), without a fragment. |
| `AllowedTenantIds` | no | When non-empty, a token from a tenant outside it is refused as `TenantNotAllowed`. It must not hold the personal accounts tenant. |
| `AuthorityBaseUrl` | no | Defaults to `https://login.microsoftonline.com`. Change it only for a national cloud, or for a local fake identity provider in tests. |
| `StateLifetime` | no | How long a begun flow can be completed, default 10 minutes. The binding cookie expires with it. |

The package calls Microsoft through a named `HttpClient`, `MicrosoftAuthServiceCollectionExtensions.HttpClientName`. Configure it with `services.AddHttpClient(MicrosoftAuthServiceCollectionExtensions.HttpClientName)` to add a proxy or a timeout. The clock is the registered `TimeProvider` (`TimeProvider.System` unless one is registered): state expiry, token lifetime and the max authentication age are all checked against it.

### Tenants

- A personal Microsoft account (tenant `9188040d-6c67-4c5b-b112-36a304b66dad`) is always refused as `PersonalAccount`.
- With no tenant in the settings, the flow uses the `organizations` authority segment (never `common`), so any work or school tenant can sign in, subject to `AllowedTenantIds`.
- With `MicrosoftSignInSettings.TenantId` set, that tenant is the authority segment, so Microsoft shows only its sign-in, and a token from another tenant is refused as `TenantMismatch`.
- The issuer is checked against the token's own tenant: it must be `{authority}/{tid}/v2.0`.

## Second factor and sign-in freshness

`MicrosoftSignInSettings` (and `MicrosoftAuthBeginRequest` for your own flows) carry two optional checks:

- **`MaxAuthenticationAge`**: Begin sends `max_age` in whole seconds, and Complete refuses a token without `auth_time` (`MissingAuthTime`) or with one older than the age (`StaleAuthTime`). The age is measured on the server's clock with no skew allowance, so keep it synchronized (or allow for its drift in the age you choose); an `auth_time` ahead of the server clock counts as no age. This proves the user entered their credentials within that time. It does not prove a second factor ran: Microsoft can satisfy an MFA requirement from an earlier MFA in the session, and `prompt=login` only forces the credentials to be entered again.
- **`RequiredAuthenticationContextId`**: Begin asks for a Conditional Access authentication context (for example `c1`) through the `claims` parameter, and Complete refuses a token whose `acrs` does not contain it (`MissingAuthenticationContext`). Conditional Access puts the id in `acrs` only when every policy assigned to it is satisfied. This is how a sign-in proves a second factor ran, and it rests on tenant setup the package cannot see from the token.

The tenant setup a required authentication context relies on:

1. Conditional Access needs Microsoft Entra ID P1 in the user's tenant.
2. Under **Conditional Access → Authentication contexts**, create the context (for example `c1`) and publish it to apps.
3. Create a Conditional Access policy targeting that authentication context, requiring multifactor authentication, with the session control *Sign-in frequency* set to *Every time*.
4. Add `acrs` as an optional ID token claim on the app registration (above).

What that cannot cover: Microsoft issues the `acrs` value even when no policy is assigned to the context, so a tenant without step 3 passes the check without any second factor. *Every time* allows five minutes of clock skew, so a second sign-in within five minutes of an MFA is not prompted again. Microsoft advises against hard-coding context ids, so take the id from the tenant's configuration per request rather than a constant.

`MicrosoftUserInfo.AuthenticationMethods` (`amr`) is passed through as given, for information only; it does not prove a second factor ran at this sign-in.

## Your own flows: linking an account, re-checking at signing

Linking a Microsoft account to a signed-in user, or re-checking identity before a sensitive action, runs from your own authenticated endpoints with `IMicrosoftAuthService` directly. Give each flow its own redirect URI, so its page knows from its route which of your endpoints to post `code` and `state` to.

```csharp
app.MapPost("/api/sign/{recordId}/microsoft/begin", (string recordId, IMicrosoftAuthService microsoft, HttpContext httpContext) =>
{
    string userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    MicrosoftAuthBeginResult begun = microsoft.Begin(new MicrosoftAuthBeginRequest
    {
        RedirectUri = "https://app.example/sign/microsoft/callback",
        CallerBinding = $"sign:{userId}:{recordId}",
        TenantId = tenantIdOfThisUser,
        Prompt = MicrosoftAuthPrompt.Login,
        RequiredAuthenticationContextId = authenticationContextIdOfThisTenant,
        MaxAuthenticationAge = TimeSpan.FromMinutes(5),
    }, httpContext);

    return Results.Ok(new { begun.AuthorizationUrl });
}).RequireAuthorization();

app.MapPost("/api/sign/{recordId}/microsoft", async (string recordId, MicrosoftSignInRequest request, IMicrosoftAuthService microsoft, HttpContext httpContext) =>
{
    string userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    MicrosoftAuthCompletionResult result = await microsoft.CompleteAsync(request.Code, request.State, httpContext, httpContext.RequestAborted);

    if (!result.Success || result.CallerBinding != $"sign:{userId}:{recordId}")
    {
        return Results.Unauthorized();
    }

    // Check that result.UserInfo.TenantId and ObjectId are the account linked to this user, then sign.
    return Results.Ok();
}).RequireAuthorization();
```

**Your Complete must check the caller binding.** The binding cookie proves the flow is completed in the browser that began it, but every flow of your application shares one state key, so a `state` from one flow opens in any other: a state from your sign-in page could be posted to your signing endpoint, or one begun for record A to the endpoint for record B. Comparing `CallerBinding` with what the completing endpoint expects is what ties the result to that flow. The built-in sign-in endpoint does this for you: it refuses any state it did not begin itself, as `CallerBindingMismatch`, so a state from your own flows can never sign anyone in.

`MicrosoftAuthBeginRequest.Nonce` lets a flow tie the sign-in to something of yours (for example a hash of the record being signed); it comes back as `MicrosoftUserInfo.Nonce`. `CompleteAsync` returns a `MicrosoftAuthFailureReason` on refusal and throws only when Microsoft's token endpoint cannot be reached or answers with a status other than 400 or 401, such as a server error or a 429 (`HttpRequestException`), or its signing keys cannot be retrieved (`InvalidOperationException`).

## Endpoint Behavior

`AddMicrosoftAuthEndpoints` maps two endpoints, sharing two properties with the built-in EasyReasy.Auth endpoints:

- They are **anonymous**, so they stay reachable even when the application applies a global authorization policy.
- They set **`Cache-Control: no-store`**, so the authorize URL and the issued token are never cached by browsers or proxies.

`POST /api/auth/microsoft/begin` takes an optional body `{ "loginHint": "..." }` and nothing else (a blank hint, or one over 256 characters, is not sent): any other field is ignored, so the browser cannot choose the tenant, prompt, authentication context, max age or redirect URI. It returns `{ "authorizationUrl": "..." }`. Settings that `Begin` refuses (a redirect URI outside `RedirectUris`, the personal accounts tenant, an undefined `Prompt` value, a blank `RequiredAuthenticationContextId`, a max age under one second) are a configuration error, and the request fails with the `ArgumentException`.

`POST /api/auth/microsoft` takes `{ "code": "...", "state": "..." }` and returns 200 with the `AuthResponse` or 401. A code and state can be completed once: the binding cookie is cleared on the first attempt, and Microsoft refuses a code redeemed before. If Microsoft's token endpoint cannot be reached or answers with a status other than 400 or 401 (a server error, a 429), or its signing keys cannot be retrieved, the request fails with a 500; that is not a refusal and is not audited.

### Access-token cookie

When `EasyReasyAuthOptions.CookieTokenPaths` is non-empty, a successful sign-in also sets the `__Secure-access_token` cookie through the same call the built-in login endpoint makes, so a user who signs in with Microsoft can open a protected file in a new tab without waiting for the first refresh. A refused sign-in sets no cookie, the JSON response is unchanged, and with the option empty nothing changes. A token too large for the cookie does not fail the sign-in: the cookie is expired instead and EasyReasy.Auth logs an error naming the size and the limit. See "Opening Files in a New Tab" in the EasyReasy.Auth README.

## Signing keys and key rotation

Microsoft's OpenID configuration and signing keys are fetched from `{authority}/organizations/v2.0/.well-known/openid-configuration` through the named `HttpClient`, cached, and refreshed periodically. When a token is signed with a key the cache does not hold yet, because Microsoft has rotated its keys, the validator fetches the keys again at once and validates once more before refusing: the code is single-use, so a refusal would send the user back to Microsoft. Only a token returned by Microsoft's token endpoint over the server's own connection reaches that fetch, so a caller cannot make the server fetch at will. A token whose key is in neither set is refused as `InvalidSignature`. Only RS256 is accepted.

## Audit Logging

If you register an `IAuthAuditLogger` (see "Security Audit Logging" in the EasyReasy.Auth README), the sign-in endpoint invokes `OnExternalAuthAsync` after every attempt, success or failure, before the response is written. The begin endpoint reports nothing, since nobody has signed in yet.

- `Provider` is `"microsoft"`.
- `AttemptedSubject` is `tid:oid` (for example `22222222-bbbb-4bbb-8bbb-222222222222:44444444-dddd-4ddd-8ddd-444444444444`) on success and whenever a genuine token was refused, including `CallerBindingMismatch`, which is refused after the token checked out; it is null when the state, the cookie, the code or the token failed, since no trustworthy account is known then.
- `FailureReason` is `InvalidToken` for a failure of the state, the binding cookie, the code or the token (including `CallerBindingMismatch`), and `Rejected` for a genuine account refused by the tenant, authentication context or max age rules, or by your handler returning null.
- `FailureDetail` is the `MicrosoftAuthFailureReason` member name for every refusal the package makes itself, and null when your handler refused. Member names are a stable contract, so they are safe to store and query.

As with the other result types, log the metadata, never the whole result: on success it embeds the issued bearer token.

### Refusal reasons

| `MicrosoftAuthFailureReason` | `FailureReason` | When |
|------------------------------|-----------------|------|
| `InvalidState` | `InvalidToken` | The state is missing, not this server's, tampered with or unreadable. |
| `ExpiredState` | `InvalidToken` | The state is older than `StateLifetime`. |
| `BindingCookieMissing` | `InvalidToken` | The browser did not send the flow's binding cookie: another browser, or a flow already completed. |
| `BindingCookieMismatch` | `InvalidToken` | The browser sent a different value for the flow's binding cookie. |
| `CodeRedemptionFailed` | `InvalidToken` | The code is missing, or Microsoft refused to redeem it, or answered without an ID token. |
| `InvalidToken` | `InvalidToken` | The ID token is malformed, not yet valid, has a `tid` that is not a GUID, has no `oid`, or has an `auth_time` that is not a number. (A token with no `tid` at all is `InvalidIssuer`.) |
| `InvalidSignature` | `InvalidToken` | The signature does not verify against Microsoft's keys, or the algorithm is not RS256. |
| `InvalidAudience` | `InvalidToken` | The audience is not `ClientId`. |
| `InvalidIssuer` | `InvalidToken` | The issuer is not the one for the token's own tenant. |
| `TokenExpired` | `InvalidToken` | The ID token has expired. |
| `NonceMismatch` | `InvalidToken` | The nonce is missing or not the flow's. |
| `CallerBindingMismatch` | `InvalidToken` | The state was begun by one of your own flows and posted to the sign-in endpoint. |
| `PersonalAccount` | `Rejected` | A personal Microsoft account. |
| `TenantMismatch` | `Rejected` | The flow named a tenant and the token is from another. |
| `TenantNotAllowed` | `Rejected` | The tenant is not in `AllowedTenantIds`. |
| `MissingAuthenticationContext` | `Rejected` | The flow required an authentication context and `acrs` lacks it. |
| `MissingAuthTime` | `Rejected` | The flow set a max age and the token has no `auth_time`. |
| `StaleAuthTime` | `Rejected` | The flow set a max age and `auth_time` is older. |

## Models

### `MicrosoftUserInfo`

| Property | Type | Description |
|----------|------|-------------|
| `TenantId` | `Guid` | The account's tenant (`tid`). Link accounts by this and `ObjectId`. |
| `ObjectId` | `string` | The account's object id in its tenant (`oid`), stable across apps. |
| `AuthenticationTime` | `DateTimeOffset?` | When the user last entered credentials (`auth_time`), or null if the token has none. |
| `Nonce` | `string` | The flow's nonce, as Begin sent it. |
| `PreferredUsername` | `string?` | The sign-in name (`preferred_username`). Mutable and not verified: display only, never an identifier. |
| `Name` | `string?` | The display name (`name`). |
| `AuthenticationMethods` | `IReadOnlyList<string>` | `amr` as given, informational. Empty when absent. |
| `AuthenticationContextIds` | `IReadOnlyList<string>` | The satisfied authentication contexts (`acrs`). Empty when absent. |

### `MicrosoftSignInSettings`

| Property | Type | Description |
|----------|------|-------------|
| `RedirectUri` | `string` (required) | The sign-in page's redirect URI, one of `RedirectUris`. |
| `TenantId` | `Guid?` | The tenant the account must belong to, or null for any work or school tenant. |
| `Prompt` | `MicrosoftAuthPrompt?` | `Login`, `None`, `Consent` or `SelectAccount`, or null to send none. |
| `RequiredAuthenticationContextId` | `string?` | The authentication context the sign-in must satisfy, or null. |
| `MaxAuthenticationAge` | `TimeSpan?` | The longest accepted time since the user entered credentials, or null. |

### `MicrosoftAuthBeginRequest`

The input to `IMicrosoftAuthService.Begin` for your own flows. `Begin` throws one `ArgumentException` listing every rule broken, and sets no cookie when it does.

| Property | Type | Description |
|----------|------|-------------|
| `RedirectUri` | `string` (required) | One of `RedirectUris`, compared exactly. The code is redeemed with this same URI. |
| `CallerBinding` | `string` (required) | Your value tying the result to the flow, returned by `CompleteAsync` untouched. Must not be null; it travels in the encrypted state, so keep it short. |
| `TenantId` | `Guid?` | The tenant the account must belong to, or null for any work or school tenant. Must not be the personal accounts tenant. |
| `Prompt` | `MicrosoftAuthPrompt?` | `Login`, `None`, `Consent` or `SelectAccount`, or null to send none. Must be a defined member. |
| `Nonce` | `string?` | Your nonce, or null for a random one. Must be null or non-blank. |
| `LoginHint` | `string?` | Fills in the account picker; proves nothing. |
| `RequiredAuthenticationContextId` | `string?` | The authentication context the sign-in must satisfy, or null. Must be null or non-blank. |
| `MaxAuthenticationAge` | `TimeSpan?` | The longest accepted time since the user entered credentials, or null. At least one second; sent in whole seconds. |

### `MicrosoftAuthBeginResult`

| Property | Type | Description |
|----------|------|-------------|
| `AuthorizationUrl` | `string` | Microsoft's authorize URL. Send the browser there with a top-level navigation. |
| `State` | `string` | The encrypted `state` in that URL, the same value Microsoft's redirect brings back. Opaque. |

### `MicrosoftAuthCompletionResult`

| Property | Type | Description |
|----------|------|-------------|
| `Success` | `bool` | Whether every check passed. |
| `UserInfo` | `MicrosoftUserInfo?` | The verified identity. Set only on success. |
| `CallerBinding` | `string?` | The binding the flow was begun with. Set only on success; compare it before acting on the identity. |
| `FailureReason` | `MicrosoftAuthFailureReason?` | Why the flow was refused. Set only on failure. |
| `AttemptedSubject` | `string?` | The account as `tid:oid`: set on success and when a genuine token was refused (tenant, authentication context or max age), null when the state, the cookie, the code or the token failed. |

### `MicrosoftSignInBeginRequest`, `MicrosoftSignInBeginResponse`, `MicrosoftSignInRequest`

The bodies of the two endpoints: `{ loginHint }`, `{ authorizationUrl }` and `{ code, state }`. They follow the standard EasyReasy serialization pattern: `ToJson()`, `FromJson(string)`, `ToString()`. `MicrosoftSignInRequest.ToString()` redacts both values, since the code can be redeemed.
