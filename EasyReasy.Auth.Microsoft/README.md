# EasyReasy.Auth.Microsoft

Sign in with Microsoft work and school accounts (Microsoft Entra ID) for the EasyReasy.Auth JWT pipeline. The server runs the authorization code flow with PKCE as a confidential client: the browser only ever carries the authorization code and an encrypted `state`, and the server redeems the code at Microsoft's token endpoint and validates the ID token itself. Your application then issues the same JWTs (and optional refresh tokens) as for any other sign-in.

Beyond signing in, the same service lets your application re-check who someone is at a sensitive moment (for example signing a record): it can require that the person entered their credentials within a given time and passed a second factor through a Conditional Access authentication context.

Personal Microsoft accounts are always refused. The package never creates users: your handler links a verified identity to one of your users by tenant id and object id, never by email.

The flow itself is [EasyReasy.Auth.OpenIdConnect](../EasyReasy.Auth.OpenIdConnect/README.md)'s, with Microsoft's tenants as its providers and the Entra rules on top. Versions 2.0.0-preview.x are previews until a security review by someone who did not write it; coming from 1.x, see "Migration from 1.x" at the end.

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

        if (refreshTokenService != null)
        {
            // Starts the refresh token family and issues the first access token the way a refresh does.
            SessionStartResult session = await refreshTokenService.StartSessionAsync(
                jwtTokenService, user.Id.ToString(), "user", [], user.Roles);
            return session.AuthResponse;
        }

        // No refresh service registered: an access token alone.
        DateTime expiresAt = DateTime.UtcNow.AddHours(1);
        string token = jwtTokenService.CreateToken(
            subject: user.Id.ToString(),
            authType: "user",
            additionalClaims: [],
            roles: user.Roles,
            expiresAt: expiresAt);
        return new AuthResponse(token, expiresAt.ToString("o"));
    }
}
```

`StartSessionAsync` is described in [EasyReasy.Auth section 8](../EasyReasy.Auth/README.md#8-refresh-tokens).

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
    body: JSON.stringify({
      code: query.get("code"),
      state: query.get("state"),
      iss: query.get("iss"), // null when Microsoft sends none
      redirectUri: window.location.origin + window.location.pathname,
    }),
  });
  // 200: an AuthResponse, exactly as from the other sign-in endpoints. 401: refused.
}
```

Both calls must reach the API over HTTPS (or http on `localhost` in development) on the same host, with the browser sending cookies: begin sets a `__Host-` binding cookie that the sign-in call must carry back. The page posts its own address as `redirectUri`, and a response posted from another page than its flow named is refused.

## Flow

1. The browser posts to `/api/auth/microsoft/begin`. The endpoint asks your handler for the sign-in's settings, and the service reads Microsoft's OpenID configuration for the flow's tenant segment, generates a PKCE verifier and a nonce, sets the flow's binding cookie, and seals the flow into an encrypted `state`.
2. The browser goes to Microsoft's authorize URL, the user signs in, and Microsoft redirects back to your redirect URI with `code` and `state` (and `iss` when Microsoft sends it) in the query (`response_mode=query`, never `form_post`, which a SameSite=Lax cookie would not survive).
3. The page posts them with its own address to `/api/auth/microsoft`. The service opens the state, checks and clears the binding cookie, checks the caller binding and the redirect URI the response arrived at, redeems the code at Microsoft's token endpoint with the client secret, the PKCE verifier and the same redirect URI, validates the ID token, checks a sent `iss` against the token's issuer (against the tenant's issuer before redeeming, for a flow begun for one tenant), and applies the flow's tenant, authentication context and max age rules.
4. Your handler receives the `MicrosoftUserInfo` and returns an `AuthResponse`, or null to refuse.
5. The attempt is reported to `IAuthAuditLogger.OnExternalAuthAsync`, and the response is returned.

## Configuration

`MicrosoftAuthOptions`:

| Property | Required | Description |
|----------|----------|-------------|
| `ClientId` | yes | The app registration's application (client) id. The ID token's audience must equal it. |
| `ClientSecret` | yes | The app registration's client secret. |
| `StateKey` | yes | Exactly 32 bytes, the AES-256 key that seals the `state`. Use a key for this alone, never your MFA-secret cipher's or the JWT secret. Copied at registration. Changing it only fails flows in progress. |
| `RedirectUris` | yes | Every redirect URI a flow may use, at least one; each must also be registered on the app's Web platform. Absolute https URLs (http only on `localhost`, a name under it, or a loopback address), without a fragment. Matching is exact. |
| `AllowedTenantIds` | no | When non-empty, a token from a tenant outside it is refused as `TenantNotAllowed`. It must not hold the personal accounts tenant. |
| `AuthorityBaseUrl` | no | Defaults to `https://login.microsoftonline.com`. Change it only for a national cloud, or for a local fake identity provider in tests. Https, or http on a loopback host. |
| `StateLifetime` | no | How long a begun flow can be completed, default 10 minutes. The binding cookie expires with it. |

The package calls Microsoft through EasyReasy.Auth.OpenIdConnect's named `HttpClient`, `OpenIdConnectCodeFlow.HttpClientName`, shared with any OpenID Connect providers of your own. Configure it with `services.AddHttpClient(OpenIdConnectCodeFlow.HttpClientName)` to add a proxy or a timeout. The clock is the registered `TimeProvider` (`TimeProvider.System` unless one is registered): state expiry, token lifetime and the max authentication age are all checked against it.

### Tenants

- A personal Microsoft account (tenant `9188040d-6c67-4c5b-b112-36a304b66dad`) is always refused as `PersonalAccount`.
- With no tenant in the settings, the flow uses the `organizations` authority segment (never `common`), so any work or school tenant can sign in, subject to `AllowedTenantIds`. The token's issuer must be the one for its own tenant, `{authority}/{tid}/v2.0`, or it is refused as `InvalidIssuer`.
- With `MicrosoftSignInSettings.TenantId` set, that tenant is the authority segment: Microsoft shows only its sign-in, and the flow uses that tenant's own OpenID configuration and issuer, `{authority}/{tenant}/v2.0`. A token issued by another tenant is refused as `InvalidIssuer`, and one issued by the tenant but carrying another tenant's `tid` as `TenantMismatch`.

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

A token that both lacks the required authentication context and is older than the max age is refused as `MissingAuthenticationContext`: the context is checked first.

## Your own flows: linking an account, re-checking at signing

Linking a Microsoft account to a signed-in user, or re-checking identity before a sensitive action, runs from your own authenticated endpoints with `IMicrosoftAuthService` directly. Give each flow its own redirect URI, so its page knows from its route which of your endpoints to post to.

```csharp
app.MapPost("/api/sign/{recordId}/microsoft/begin", async (string recordId, IMicrosoftAuthService microsoft, HttpContext httpContext) =>
{
    string userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    OpenIdConnectAuthBeginResult begun = await microsoft.BeginAsync(new MicrosoftAuthBeginRequest
    {
        RedirectUri = "https://app.example/sign/microsoft/callback",
        CallerBinding = $"sign:{userId}:{recordId}",
        TenantId = tenantIdOfThisUser,
        Prompt = OpenIdConnectPrompt.Login,
        RequiredAuthenticationContextId = authenticationContextIdOfThisTenant,
        MaxAuthenticationAge = TimeSpan.FromMinutes(5),
    }, httpContext, httpContext.RequestAborted);

    return Results.Ok(new { begun.AuthorizationUrl });
}).RequireAuthorization();

app.MapPost("/api/sign/{recordId}/microsoft", async (string recordId, OpenIdConnectAuthorizationResponse response, IMicrosoftAuthService microsoft, HttpContext httpContext) =>
{
    string userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    MicrosoftAuthCompletionResult result = await microsoft.CompleteAsync(response, $"sign:{userId}:{recordId}", httpContext, httpContext.RequestAborted);

    if (!result.Success)
    {
        return Results.Unauthorized();
    }

    // Check that result.UserInfo.TenantId and ObjectId are the account linked to this user, then sign,
    // sealing result.IdToken and result.IdTokenSigningKey with the record (see "Signing evidence" below).
    return Results.Ok();
}).RequireAuthorization();
```

**Each endpoint completes only its own flows.** The binding cookie proves the flow is completed in the browser that began it, but every flow of your application shares one state key, so a `state` from one flow opens in any other: a state from your sign-in page could be posted to your signing endpoint, or one begun for record A to the endpoint for record B. `CompleteAsync` takes the caller binding the completing endpoint expects and refuses a flow begun with another as `CallerBindingMismatch`, before its code is redeemed. The built-in sign-in endpoint passes its own binding, so a state from your own flows can never sign anyone in.

`MicrosoftAuthBeginRequest.Nonce` lets a flow tie the sign-in to something of yours (for example a hash of the record being signed); it comes back as `MicrosoftUserInfo.Nonce`.

**Signing evidence.** On success, `MicrosoftAuthCompletionResult.IdToken` is the raw ID token the identity was read from, after every check passed. It carries Microsoft's own signature over the account, the sign-in time, the authentication contexts and your nonce, so a record signed through a re-check can later be shown to rest on a token Microsoft signed, not only on your server's word. Seal it with the record (encrypted, access audited) and never log it or send it to a browser: until it expires it is a bearer credential for your client id. Keep `IdTokenSigningKey`, Microsoft's public key the token verified with as a JSON Web Key, with it: Microsoft rotates and retires keys, and the saved key is what lets the signature be checked after that. The result's `ToString()` shows the token as `[REDACTED]` and its JSON leaves out the token, the key and `UserInfo`, and the sign-in endpoints never pass the token on: your `IMicrosoftAuthHandler` and the audit record do not see it.

`CompleteAsync` returns a `MicrosoftAuthFailureReason` on refusal. It throws when Microsoft's token endpoint cannot be reached or answers with a status other than 400 or 401, such as a server error or a 429 (`HttpRequestException`), when Microsoft's OpenID configuration or signing keys cannot be retrieved, and when the token endpoint refuses the client itself, such as `invalid_client` for an expired client secret (`InvalidOperationException`). `BeginAsync` reads the OpenID configuration too, so it throws `InvalidOperationException` while Microsoft is unreachable.

## Endpoint Behavior

`AddMicrosoftAuthEndpoints` maps two endpoints, sharing two properties with the built-in EasyReasy.Auth endpoints:

- They are **anonymous**, so they stay reachable even when the application applies a global authorization policy.
- They set **`Cache-Control: no-store`**, so the authorize URL and the issued token are never cached by browsers or proxies.

`POST /api/auth/microsoft/begin` takes an optional body `{ "loginHint": "..." }` and nothing else (a blank hint, or one over 256 characters, is not sent): any other field is ignored, so the browser cannot choose the tenant, prompt, authentication context, max age or redirect URI. It returns `{ "authorizationUrl": "..." }`. Settings that `BeginAsync` refuses (a redirect URI outside `RedirectUris`, the personal accounts tenant, an undefined `Prompt` value, a blank `RequiredAuthenticationContextId`, a max age under one second) are a configuration error, and the request fails with the `ArgumentException`. While Microsoft's OpenID configuration cannot be retrieved, the request fails with a 500.

`POST /api/auth/microsoft` takes `{ "code": "...", "state": "...", "iss": "...", "redirectUri": "..." }` and returns 200 with the `AuthResponse` or 401. It accepts only a `state` its own begin endpoint issued. A code and state can be completed once: the binding cookie is cleared on the first attempt, and Microsoft refuses a code redeemed before. If Microsoft's token endpoint cannot be reached or answers with a status other than 400 or 401 (a server error, a 429), refuses the client itself, or its OpenID configuration or signing keys cannot be retrieved, the request fails with a 500; that is not a refusal and is not audited.

### Access-token cookie

When `EasyReasyAuthOptions.CookieTokenPaths` is non-empty, a successful sign-in also sets the `__Secure-access_token` cookie through the same call the built-in login endpoint makes, so a user who signs in with Microsoft can open a protected file in a new tab without waiting for the first refresh. A refused sign-in sets no cookie, the JSON response is unchanged, and with the option empty nothing changes. A token too large for the cookie does not fail the sign-in: the cookie is expired instead and EasyReasy.Auth logs an error naming the size and the limit. See "Opening Files in a New Tab" in the EasyReasy.Auth README.

### Binding cookie

Begin sets a `__Host-easyreasy-oidc-` cookie (HttpOnly, Secure, SameSite=Lax) for each flow, through EasyReasy.Auth's `FlowBindingCookie`. A browser holds at most five pending flows across Microsoft and every OpenID Connect provider of your own; beginning a sixth clears the pending ones, whose completion then fails as `BindingCookieMissing`.

## Signing keys and key rotation

Microsoft's OpenID configuration and signing keys are fetched for the flow's tenant segment, from `{authority}/organizations/v2.0/.well-known/openid-configuration` or `{authority}/{tenant}/v2.0/.well-known/openid-configuration`, through the named `HttpClient`, cached, and refreshed periodically. When a token is signed with a key the cache does not hold yet, because Microsoft has rotated its keys, the validator fetches the keys again at once and validates once more before refusing: the code is single-use, so a refusal would send the user back to Microsoft. Only a token returned by Microsoft's token endpoint over the server's own connection reaches that fetch, so a caller cannot make the server fetch at will. A token whose key is in neither set is refused as `InvalidSignature`. Only RS256 is accepted.

## Audit Logging

If you register an `IAuthAuditLogger` (see "Security Audit Logging" in the EasyReasy.Auth README), the sign-in endpoint invokes `OnExternalAuthAsync` after every attempt, success or failure, before the response is written. The begin endpoint reports nothing, since nobody has signed in yet.

- `Provider` is `"microsoft"`.
- `AttemptedSubject` is `tid:oid` (for example `22222222-bbbb-4bbb-8bbb-222222222222:44444444-dddd-4ddd-8ddd-444444444444`) on success and whenever a genuine token was refused by the tenant, authentication context or max age rules; it is null when the state, the cookie, the caller binding, the response, the code or the token failed, since no trustworthy account is known then.
- `FailureReason` is `InvalidToken` for a failure of those, and `Rejected` for a genuine account refused by the tenant, authentication context or max age rules, or by your handler returning null.
- `FailureDetail` is the `MicrosoftAuthFailureReason` member name for every refusal the package makes itself, and null when your handler refused. Member names are a stable contract, so they are safe to store and query.

As with the other result types, log the metadata, never the whole result: on success it embeds the issued bearer token.

### Refusal reasons

| `MicrosoftAuthFailureReason` | `FailureReason` | When |
|------------------------------|-----------------|------|
| `InvalidState` | `InvalidToken` | The state is missing, not this server's, tampered with or unreadable. |
| `ExpiredState` | `InvalidToken` | The state is older than `StateLifetime`. |
| `BindingCookieMissing` | `InvalidToken` | The browser did not send the flow's binding cookie: another browser, a completed flow, or one cleared by beginning too many. |
| `BindingCookieMismatch` | `InvalidToken` | The browser sent a different value for the flow's binding cookie. |
| `CallerBindingMismatch` | `InvalidToken` | The flow was begun with another caller binding than the completing endpoint expects, such as one of your own flows posted to the sign-in endpoint. Refused before the code is redeemed. |
| `RedirectUriMismatch` | `InvalidToken` | The response was posted from another redirect URI than the flow named, or without one. |
| `IssuerParameterMismatch` | `InvalidToken` | The `iss` parameter is not the flow's issuer. |
| `IssuerParameterMissing` | `InvalidToken` | Microsoft says it sends `iss`, and the response has none. |
| `ProviderChanged` | `InvalidToken` | `ClientId` or `AuthorityBaseUrl` changed between Begin and Complete. |
| `CodeRedemptionFailed` | `InvalidToken` | The code is missing, or Microsoft refused to redeem it as `invalid_grant` or `invalid_request`, or answered without an ID token. A refusal of the client itself throws instead. |
| `InvalidToken` | `InvalidToken` | The ID token is malformed, not yet valid, lacks a `tid` or has one that is not a GUID, has no `oid`, or has an `auth_time` that is not a number. (On a flow that named no tenant, a token with no `tid` is `InvalidIssuer`.) |
| `InvalidSignature` | `InvalidToken` | The signature does not verify against Microsoft's keys, or the algorithm is not RS256. |
| `InvalidAudience` | `InvalidToken` | The audience is not `ClientId`. |
| `InvalidIssuer` | `InvalidToken` | The issuer is not the flow's: the token's own tenant's on a flow that named no tenant, that tenant's on one that did. |
| `TokenExpired` | `InvalidToken` | The ID token has expired. |
| `NonceMismatch` | `InvalidToken` | The nonce is missing or not the flow's. |
| `PersonalAccount` | `Rejected` | A personal Microsoft account. |
| `TenantMismatch` | `Rejected` | The flow named a tenant, and the token, issued by that tenant, carries another `tid`. |
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
| `Prompt` | `OpenIdConnectPrompt?` | `Login`, `None`, `Consent` or `SelectAccount`, or null to send none. |
| `RequiredAuthenticationContextId` | `string?` | The authentication context the sign-in must satisfy, or null. |
| `MaxAuthenticationAge` | `TimeSpan?` | The longest accepted time since the user entered credentials, or null. |

### `MicrosoftAuthBeginRequest`

The input to `IMicrosoftAuthService.BeginAsync` for your own flows, which returns EasyReasy.Auth.OpenIdConnect's `OpenIdConnectAuthBeginResult`: the `AuthorizationUrl` to send the browser to with a top-level navigation, and the encrypted `State` in it. `BeginAsync` throws an `ArgumentException` listing the rules broken, the Entra ones (the personal accounts tenant, a blank authentication context id) reported first on their own, and sets no cookie when it does.

| Property | Type | Description |
|----------|------|-------------|
| `RedirectUri` | `string` (required) | One of `RedirectUris`, compared exactly. The code is redeemed with this same URI, and completion refuses a response posted from another. |
| `CallerBinding` | `string` (required) | What the flow is for; your completing endpoint passes the value it expects to `CompleteAsync`. Must not be null; it travels in the encrypted state, so keep it short. |
| `TenantId` | `Guid?` | The tenant the account must belong to, or null for any work or school tenant. Must not be the personal accounts tenant. |
| `Prompt` | `OpenIdConnectPrompt?` | `Login`, `None`, `Consent` or `SelectAccount`, or null to send none. Must be a defined member. |
| `Nonce` | `string?` | Your nonce, or null for a random one. Must be null or non-blank. |
| `LoginHint` | `string?` | Fills in the account picker; proves nothing. |
| `RequiredAuthenticationContextId` | `string?` | The authentication context the sign-in must satisfy, or null. Must be null or non-blank. |
| `MaxAuthenticationAge` | `TimeSpan?` | The longest accepted time since the user entered credentials, or null. At least one second; sent in whole seconds. |

### `MicrosoftAuthCompletionResult`

| Property | Type | Description |
|----------|------|-------------|
| `Success` | `bool` | Whether every check passed. |
| `UserInfo` | `MicrosoftUserInfo?` | The verified identity. Set only on success. Personal data: left out of `ToString()` and JSON. |
| `IdToken` | `string?` | The validated raw ID token, as signing evidence to seal with the record. Set only on success. Never log it; `ToString()` shows it as `[REDACTED]` and JSON leaves it out. |
| `IdTokenSigningKey` | `string?` | Microsoft's public key the token verified with, as JWK JSON, to keep with the token. Set only on success; left out of JSON. |
| `FailureReason` | `MicrosoftAuthFailureReason?` | Why the flow was refused. Set only on failure. |
| `AttemptedSubject` | `string?` | The account as `tid:oid`: set on success and when a genuine token was refused (tenant, authentication context or max age), null otherwise. |

### Endpoint bodies

The begin endpoint takes `MicrosoftSignInBeginRequest` (`{ loginHint }`) and returns `MicrosoftSignInBeginResponse` (`{ authorizationUrl }`). The sign-in endpoint takes EasyReasy.Auth.OpenIdConnect's `OpenIdConnectAuthorizationResponse` (`{ code, state, iss, redirectUri }`), whose `ToString()` redacts the code and state. They follow the standard EasyReasy serialization pattern: `ToJson()`, `FromJson(string)`, `ToString()`.

## Migration from 1.x

2.0 runs on EasyReasy.Auth.OpenIdConnect's shared flow. The `tid` plus `oid` identity, the options, the handler and the endpoint paths are unchanged, and every refusal reason keeps its name, so stored account links and audit records stay valid. What changes:

1. **Some types now come from EasyReasy.Auth.OpenIdConnect**, which the package depends on: `OpenIdConnectAuthBeginResult`, `OpenIdConnectAuthorizationResponse`, `OpenIdConnectPrompt` (also the type of `MicrosoftSignInSettings.Prompt`, so every handler meets it) and `OpenIdConnectCodeFlow`. Add `using EasyReasy.Auth.OpenIdConnect;` where you use them.
2. **The frontend posts more.** The page at the redirect URI posts `iss` and its own address as `redirectUri` with `code` and `state` (see Quick Start step 5). A frontend that posts only `code` and `state` is refused as `RedirectUriMismatch`.
3. **`Begin` is `BeginAsync`** and returns `OpenIdConnectAuthBeginResult` (same `AuthorizationUrl` and `State`) instead of `MicrosoftAuthBeginResult`, since it reads Microsoft's OpenID configuration.
4. **`CompleteAsync(code, state, httpContext)` is `CompleteAsync(response, expectedCallerBinding, httpContext)`**, taking an `OpenIdConnectAuthorizationResponse` and the caller binding your endpoint expects. The result no longer has `CallerBinding`: drop your own comparison, since a flow begun with another binding is refused as `CallerBindingMismatch`. `MicrosoftSignInRequest` is gone; your endpoints bind `OpenIdConnectAuthorizationResponse`.
5. **`MicrosoftAuthPrompt` is `OpenIdConnectPrompt`**, with the same members.
6. **The named `HttpClient`** is `OpenIdConnectCodeFlow.HttpClientName`, replacing `MicrosoftAuthServiceCollectionExtensions.HttpClientName`.
7. **Signing evidence**: keep `IdTokenSigningKey` with a sealed `IdToken` instead of looking the key up by `kid` yourself.
8. **New refusal reasons** `RedirectUriMismatch`, `IssuerParameterMismatch`, `IssuerParameterMissing` and `ProviderChanged` appear in `FailureDetail`.
9. **Behaviour**:
   - A refusal of the client itself (`invalid_client`, such as an expired client secret) now throws instead of returning `CodeRedemptionFailed`, so it surfaces as a 500 rather than a refused user.
   - On a flow that named a tenant, a token issued by another tenant is now `InvalidIssuer` with no subject, where it was `TenantMismatch` with the account.
   - `CallerBindingMismatch` is now refused before the code is redeemed, with no subject.
   - A token both lacking the required authentication context and older than the max age is still `MissingAuthenticationContext`.
   - The binding cookie is now named `__Host-easyreasy-oidc-`, and its limit of five pending flows is shared with your own OpenID Connect providers. Flows begun before the upgrade fail once, as `BindingCookieMissing`.
