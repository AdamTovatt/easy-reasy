# EasyReasy.Auth.OpenIdConnect

Sign in with any OpenID Connect provider (Okta, Ping, Google Workspace, Keycloak and the like) for the EasyReasy.Auth JWT pipeline. The server runs the authorization code flow with PKCE as a confidential client: the browser only ever carries the authorization code and an encrypted `state`, and the server redeems the code at the provider's token endpoint and validates the ID token itself. Your application then issues the same JWTs (and optional refresh tokens) as for any other sign-in.

Providers are not configuration: your application resolves them per request by a key it chooses (for example `okta`, or a customer's id), so a provider can be added or changed without a restart. Beyond signing in, the same service lets your application re-check who someone is at a sensitive moment (for example signing a record): it can require that the person authenticated within a given time, as a given account, with given `acr` or `amr` values or claims.

The package never creates users. An identity is the issuer plus `sub`: your handler links it to one of your users by that pair, never by email.

Versions 1.0.0-preview.x are previews until a security review by someone who did not write it.

## Quick Start

### 1. Install

```bash
dotnet add package EasyReasy.Auth.OpenIdConnect
```

### 2. Register a client at the provider

For each provider, register a confidential web client (the provider may call it a *web application* or *server-side* client):

1. Enable the **authorization code** grant only. Leave the implicit and password grants off.
2. Add each redirect URI your application uses for this provider: one page per flow, for example `https://app.example/auth/okta/callback` for sign-in and `https://app.example/sign/okta/callback` for a re-check at signing. Give every provider redirect URIs of its own, never shared with another provider (see "Mix-up" below).
3. Create a client secret and store it as a server secret. The secret is sent as `client_secret_basic` by default or `client_secret_post`; private key JWT and mutual TLS are not supported.
4. Require PKCE with S256 if the provider lets you; the package always sends it.

The provider's issuer is the URL its discovery document names as `issuer`, for example `https://acme.okta.com` or `https://keycloak.example/realms/acme`. The discovery document must be at the issuer followed by `/.well-known/openid-configuration`, as the specification places it.

### 3. Implement `IOpenIdConnectProviderResolver`

The resolver supplies a provider by key. It is called when a flow begins and again when it completes (the client secret is never put in the `state`), and never with a blank key:

```csharp
public class MyProviderResolver : IOpenIdConnectProviderResolver
{
    private readonly IIdentityProviderRepository _providers;

    public MyProviderResolver(IIdentityProviderRepository providers)
    {
        _providers = providers;
    }

    public async Task<OpenIdConnectProvider?> FindProviderAsync(string providerKey, CancellationToken cancellationToken)
    {
        IdentityProviderRecord? record = await _providers.FindAsync(providerKey, cancellationToken);
        if (record == null)
        {
            return null;
        }

        return new OpenIdConnectProvider(
            OpenIdConnectIssuer.Fixed(record.Issuer),
            record.ClientId,
            record.ClientSecret,
            [$"https://app.example/auth/{providerKey}/callback", $"https://app.example/sign/{providerKey}/callback"]);
    }
}
```

The key reaching the begin endpoint comes from the browser, so treat it as untrusted. `OpenIdConnectIssuer.Fixed` throws an `ArgumentException` for an issuer that breaks its rule, and `OpenIdConnectProvider`'s constructor checks every other value and throws one `ArgumentException` listing every problem, so a resolver can only return a usable provider.

### 4. Implement `IOpenIdConnectAuthHandler`

The handler decides whether a provider is offered for sign-in and the rules a sign-in is begun with, and what happens once an identity is verified:

```csharp
public class MyOpenIdConnectAuthHandler : IOpenIdConnectAuthHandler
{
    private readonly IIdentityProviderRepository _providers;
    private readonly IUserRepository _users;

    public MyOpenIdConnectAuthHandler(IIdentityProviderRepository providers, IUserRepository users)
    {
        _providers = providers;
        _users = users;
    }

    public async Task<OpenIdConnectSignInSettings?> GetSignInSettingsAsync(string providerKey, HttpContext httpContext)
    {
        // Null for a key the application does not offer for sign-in: the begin endpoint answers 400.
        IdentityProviderRecord? record = await _providers.FindAsync(providerKey, httpContext.RequestAborted);
        if (record == null || !record.OfferedForSignIn)
        {
            return null;
        }

        return new OpenIdConnectSignInSettings
        {
            RedirectUri = $"https://app.example/auth/{providerKey}/callback",
        };
    }

    public async Task<AuthResponse?> HandleOpenIdConnectUserAsync(
        OpenIdConnectUserInfo userInfo,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService? refreshTokenService,
        HttpContext httpContext)
    {
        // Link by issuer and sub, never by email.
        User? user = await _users.FindByOpenIdConnectIdentityAsync(userInfo.Identity.Issuer, userInfo.Identity.Subject);
        if (user == null)
        {
            return null; // not linked: the sign-in is refused and audited as Rejected
        }

        if (refreshTokenService != null)
        {
            SessionStartResult session = await refreshTokenService.StartSessionAsync(
                jwtTokenService, user.Id.ToString(), "user", [], user.Roles);
            return session.AuthResponse;
        }

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

`StartSessionAsync` is described in [EasyReasy.Auth section 8](../EasyReasy.Auth/README.md#8-refresh-tokens). `GetSignInSettingsAsync` is asked before the key is resolved, since the settings are part of the begin request, so it also sees keys no provider has: return null for those.

### 5. Register services and endpoints

```csharp
// In Program.cs
builder.Services.AddEasyReasyAuth(jwtSecret);
builder.Services.AddEasyReasyOpenIdConnectAuth(new OpenIdConnectAuthOptions
{
    StateKey = openIdConnectStateKey, // 32 random bytes, used for nothing else
});
builder.Services.AddScoped<IOpenIdConnectProviderResolver, MyProviderResolver>();
builder.Services.AddScoped<IOpenIdConnectAuthHandler, MyOpenIdConnectAuthHandler>();

WebApplication app = builder.Build();

app.UseEasyReasyAuth();
app.AddOpenIdConnectAuthEndpoints(); // POST /api/auth/oidc/begin and POST /api/auth/oidc
```

`IOpenIdConnectAuthService` is scoped, so the resolver may be scoped too. Startup fails with one `ArgumentException` listing every problem with the options, and `AddOpenIdConnectAuthEndpoints` fails at startup when the OpenID Connect services, your resolver, your handler or EasyReasy.Auth's `IJwtTokenService` is not registered.

### 6. Call it from the frontend

```javascript
// The sign-in button for one provider.
const begin = await fetch("/api/auth/oidc/begin", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ providerKey: "okta", loginHint: null }),
});
const { authorizationUrl } = await begin.json();
window.location.assign(authorizationUrl);

// The page at the redirect URI.
const query = new URLSearchParams(window.location.search);
if (query.has("error")) {
  // The provider refused or the person cancelled; show query.get("error_description").
} else {
  const response = await fetch("/api/auth/oidc", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      code: query.get("code"),
      state: query.get("state"),
      iss: query.get("iss"), // null when the provider sends none
      redirectUri: window.location.origin + window.location.pathname,
    }),
  });
  // 200: an AuthResponse, exactly as from the other sign-in endpoints. 401: refused.
}
```

Both calls must reach the API over HTTPS (or http on `localhost` in development) on the same host, with the browser sending cookies: begin sets a `__Host-` binding cookie that the sign-in call must carry back.

## Flow

1. The browser posts the provider key to `/api/auth/oidc/begin`. The endpoint asks your handler for the sign-in's settings, the service resolves the provider and reads its discovery document, generates a PKCE verifier and a nonce, sets the flow's binding cookie, and seals the flow into an encrypted `state`.
2. The browser goes to the provider's authorize URL, the person signs in, and the provider redirects back to your redirect URI with `code`, `state` and usually `iss` in the query (`response_mode=query`, never `form_post`, which a SameSite=Lax cookie would not survive).
3. The page posts them with its own address to `/api/auth/oidc`. The service opens the state, checks and clears the binding cookie, checks the caller binding and the redirect URI the response arrived at, resolves the provider again and checks it is the same issuer and client, checks `iss`, redeems the code with the client secret, the PKCE verifier and the same redirect URI, validates the ID token, and applies the flow's rules.
4. Your handler receives the `OpenIdConnectUserInfo` and returns an `AuthResponse`, or null to refuse.
5. The attempt is reported to `IAuthAuditLogger.OnExternalAuthAsync`, and the response is returned.

The flow keeps no record of begun or used flows. A code can be redeemed once because the provider refuses a second redemption, as the specification requires; the binding cookie is cleared at the first completion, and the PKCE verifier and nonce are sealed in the `state`. The package's tests check that Keycloak refuses a second redemption. An application that needs an audit of every sign-in writes it from the completion result.

## Providers

`OpenIdConnectProvider`:

| Constructor argument | Required | Description |
|----------------------|----------|-------------|
| `issuer` | yes | `OpenIdConnectIssuer.Fixed("https://acme.okta.com")`. Absolute https (http only on `localhost` or a loopback address), without a query or fragment. The discovery document's `issuer` and every ID token's `iss` must equal it exactly. |
| `clientId` | yes | The client id registered at the provider. An ID token's audience must hold it, and its `azp`, when present, must be it. |
| `clientSecret` | yes | The client secret. It never reaches the browser. |
| `redirectUris` | yes | Every redirect URI a flow with this provider may name, each registered at the provider, at least one. Absolute https (http only on `localhost`, a name under it or a loopback address), written exactly as the page posts it, its origin plus path: a lowercase host, no default port, no query or fragment. Matching is exact. |
| `clientAuthentication` | no | An `OpenIdConnectClientAuthentication`: `ClientSecretBasic` (default) or `ClientSecretPost`. |
| `signingAlgorithms` | no | The algorithms an ID token may be signed with, from RS, PS and ES at 256, 384 and 512. Defaults to RS256 alone. HMAC is never accepted: it is keyed with the client secret, so anyone holding the secret could mint a token. |
| `scope` | no | Space-separated, holding `openid`. Defaults to `openid profile email`. |

`OpenIdConnectProvider.FindProblems(...)` returns the same problems the constructor throws for, for an application that checks its stored providers ahead of time. It takes every argument but the issuer, so check a stored issuer by calling `OpenIdConnectIssuer.Fixed` on it.

`OpenIdConnectIssuer.TenantTemplate(template, tenantClaim, metadataAddress)` is the other form of issuer: a multi-tenant template such as Microsoft Entra ID's `organizations` endpoint, where each token's `iss` is the template with the token's own tenant claim in place of `{tenantid}`. It is for provider packages (EasyReasy.Auth.Microsoft builds one); a provider your application configures is a fixed issuer.

### Mix-up

A provider must not be able to pass off its response as another's. Completion refuses a response that arrived at another redirect URI than the flow named (`RedirectUriMismatch`) before anything is sent to a provider, and that only stops a mix-up while no two providers share a redirect URI: give each its own. When the response carries the `iss` parameter (RFC 9207), it must be the flow's provider's issuer (`IssuerParameterMismatch`), and when the provider's discovery document says it sends one, a response without it is refused (`IssuerParameterMissing`).

A flow's provider is resolved again at completion, and a provider whose issuer or client id changed since the flow began is refused as `ProviderChanged`; a key that no longer resolves is `UnknownProvider`.

## Configuration

`OpenIdConnectAuthOptions`:

| Property | Required | Description |
|----------|----------|-------------|
| `StateKey` | yes | Exactly 32 bytes, the AES-256 key that seals the `state`. Use a key for this alone, never your MFA-secret cipher's, the JWT secret or another package's state key. Copied at registration. Changing it only fails flows in progress. |
| `StateLifetime` | no | How long a begun flow can be completed, default 10 minutes. The binding cookie expires with it. |

The package calls providers through a named `HttpClient`, `OpenIdConnectCodeFlow.HttpClientName`. Configure it with `services.AddHttpClient(OpenIdConnectCodeFlow.HttpClientName)` to add a proxy or a timeout. The clock is the registered `TimeProvider` (`TimeProvider.System` unless one is registered): state expiry, token lifetime and the max authentication age are all checked against it.

## Re-checking a sign-in

`OpenIdConnectSignInSettings` (and `OpenIdConnectAuthBeginRequest` for your own flows) carry the rules a flow is begun with. Each is sent to the provider and checked again on the ID token, so a provider that ignores a request is still refused:

- **`MaxAuthenticationAge`**: sent as `max_age` in whole seconds, with `auth_time` asked for as an essential claim. A token without `auth_time` is refused as `MissingAuthTime`, and one older than the age as `StaleAuthTime`. The age is the server clock's now minus `auth_time` with no skew allowance, so keep the clock synchronized; an `auth_time` ahead of the server clock counts as no age. This proves the person authenticated within that time, not that a second factor ran.
- **`AcceptedAcrValues`**: asked for as an essential `acr` claim with these values. A token without `acr` is `MissingAcr`, and one with another value `AcrNotAccepted`.
- **`AcceptedAmrValues`**: asked for as an essential `amr` claim. A token without `amr` is `MissingAmr`, and one holding none of these `AmrNotAccepted`. With both `acr` and `amr` values set, the token must meet both.
- **`RequiredClaimValues`**: further claims with the one value each must have (equal, or as an array, holding it), asked for as essential claims. A missing claim is `MissingRequiredClaim`, another value `RequiredClaimMismatch`. Names may not be `auth_time`, `acr` or `amr`.
- **`ExpectedIdentity`** (begin request only): the account the person must sign in as, for a re-check of a signed-in user. Another account is `IdentityMismatch`.

A matching `acr` or `amr` proves what the provider asserts, not that a factor ran for this act: a provider may report a factor from earlier in its session. Which values a provider sends, and whether it honours `prompt` and `max_age`, is the provider's property, so check its documentation.

## Your own flows: linking an account, re-checking at signing

Linking a provider account to a signed-in user, or re-checking identity before a sensitive action, runs from your own authenticated endpoints with `IOpenIdConnectAuthService` directly. Give each flow its own redirect URI, so its page knows from its route which of your endpoints to post to.

```csharp
app.MapPost("/api/sign/{recordId}/oidc/begin", async (string recordId, ILinkedAccountRepository accounts, IOpenIdConnectAuthService oidc, HttpContext httpContext) =>
{
    string userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    LinkedAccount account = await accounts.FindAsync(userId);
    OpenIdConnectAuthBeginResult? begun = await oidc.BeginAsync(new OpenIdConnectAuthBeginRequest
    {
        ProviderKey = account.ProviderKey,
        RedirectUri = $"https://app.example/sign/{account.ProviderKey}/callback",
        CallerBinding = $"sign:{userId}:{recordId}",
        Prompt = OpenIdConnectPrompt.Login,
        MaxAuthenticationAge = TimeSpan.FromMinutes(5),
        ExpectedIdentity = new OpenIdConnectIdentity(account.Issuer, account.Subject),
        Nonce = hashOfThisRecord, // comes back as UserInfo.Nonce
    }, httpContext, httpContext.RequestAborted);

    return begun == null ? Results.BadRequest() : Results.Ok(new { begun.AuthorizationUrl });
}).RequireAuthorization();

app.MapPost("/api/sign/{recordId}/oidc", async (string recordId, OpenIdConnectAuthorizationResponse response, IOpenIdConnectAuthService oidc, HttpContext httpContext) =>
{
    string userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    OpenIdConnectAuthCompletionResult result = await oidc.CompleteAsync(response, $"sign:{userId}:{recordId}", httpContext, httpContext.RequestAborted);

    if (!result.Success)
    {
        return Results.Unauthorized();
    }

    // Sign, sealing result.IdToken and result.IdTokenSigningKey with the record (see "Signing evidence" below).
    return Results.Ok();
}).RequireAuthorization();
```

`BeginAsync` returns null for a key no provider has, and throws one `ArgumentException` listing every rule the request breaks (setting no cookie); the rules are in [`OpenIdConnectAuthBeginRequest`](#openidconnectauthbeginrequest). Every flow of your application shares one state key, so a `state` from one flow opens in any other: a state from your sign-in page could be posted to your signing endpoint, or one begun for record A to the endpoint for record B. **`CompleteAsync` takes the caller binding the completing endpoint expects** and refuses a flow begun with another as `CallerBindingMismatch`, before its code is redeemed, so each endpoint completes only its own flows. The built-in sign-in endpoint passes its own binding, so a state from your own flows can never sign anyone in.

`OpenIdConnectAuthBeginRequest.Nonce` ties the sign-in to something of yours (for example a hash of the record being signed); it comes back as `OpenIdConnectUserInfo.Nonce`.

**Signing evidence.** On success, `OpenIdConnectAuthCompletionResult.IdToken` is the raw ID token the identity was read from, after every check passed: the provider's own signature over the account, the sign-in time, `acr`, `amr` and your nonce. Seal it with the record (encrypted, access audited) and never log it or send it to a browser: until it expires it is a bearer credential for your client id. Keep `IdTokenSigningKey`, the provider's public key it verified with as a JSON Web Key (with the provider's `x5c` certificates when its key set has them), with it: providers drop a key from their key set once they rotate it out, and the saved key is what lets the signature be checked after that. A later check shows the token was signed by that key; that the key was the provider's rests on your record that this server read it from the provider's key set at sign-in, so seal the key with the token and the record. The result's `ToString()` shows the token as `[REDACTED]`, its JSON leaves out the token, the key and `UserInfo`, and the sign-in endpoints never pass the token on.

`CompleteAsync` returns an `OpenIdConnectAuthFailureReason` on refusal. It throws when the provider's token endpoint cannot be reached or answers with a status other than 400 or 401, such as a server error or a 429 (`HttpRequestException`), when the discovery document or keys cannot be retrieved, and when the token endpoint refuses the client itself, such as `invalid_client` for an expired secret (`InvalidOperationException`): an outage or a configuration error is not a refusal of the person.

## Endpoint Behavior

`AddOpenIdConnectAuthEndpoints` maps two endpoints, sharing two properties with the built-in EasyReasy.Auth endpoints:

- They are **anonymous**, so they stay reachable even when the application applies a global authorization policy.
- They set **`Cache-Control: no-store`**, so the authorize URL and the issued token are never cached by browsers or proxies.

It returns `OpenIdConnectAuthEndpoints`, one builder per endpoint, so you can add a rate limiting policy or other metadata to one of them without matching paths:

```csharp
OpenIdConnectAuthEndpoints endpoints = app.AddOpenIdConnectAuthEndpoints();
endpoints.Begin.RequireRateLimiting("sign-in");
```

`POST /api/auth/oidc/begin` takes `{ "providerKey": "...", "loginHint": "..." }` (the hint optional; a blank one, or one over 256 characters, is not sent, by `OpenIdConnectLoginHint.FromBrowser`) and nothing else: the browser chooses the provider, never the redirect URI, prompt, max age or accepted values. It returns `{ "authorizationUrl": "..." }`, or 400 when the key is missing or blank, your handler does not offer it, or it resolves to no provider. Settings that `BeginAsync` refuses are a configuration error, and the request fails with the `ArgumentException`.

`POST /api/auth/oidc` takes `{ "code": "...", "state": "...", "iss": "...", "redirectUri": "..." }` and returns 200 with the `AuthResponse` or 401. It accepts only a `state` its own begin endpoint issued. If the provider's token endpoint cannot be reached or refuses the client, or its discovery document or keys cannot be retrieved, the request fails with a 500; that is not a refusal and is not audited.

### Access-token cookie

When `EasyReasyAuthOptions.CookieTokenPaths` is non-empty, a successful sign-in also sets the `__Secure-access_token` cookie through the same call the built-in login endpoint makes. See "Opening Files in a New Tab" in the EasyReasy.Auth README.

### Binding cookie

Begin sets a `__Host-easyreasy-oidc-` cookie (HttpOnly, Secure, SameSite=Lax) for each flow, through EasyReasy.Auth's `FlowBindingCookie`. A browser holds at most five pending flows across every OpenID Connect provider, EasyReasy.Auth.Microsoft's included; beginning a sixth clears the pending ones, whose completion then fails as `BindingCookieMissing`.

## Discovery and signing keys

Each provider's discovery document and keys are fetched through the named `HttpClient`, cached per discovery address and issuer, and refreshed periodically. A document is checked before it is used or cached: its `issuer` must be the provider's, and its authorize endpoint must be https (or http on `localhost`, a name under it or a loopback address), and its token endpoint https. Plain http on a loopback host is allowed for the token endpoint and key set only when the provider's own discovery address is one, as a local test provider's is, so a remote provider's document cannot point the server at this machine's own services. When a token names a key the cache does not hold yet, because the provider rotated its keys, the keys are fetched again at once and the token validated once more before it is refused: the code is single-use, so a refusal would send the person back to the provider. Only a token returned by the token endpoint over the server's own connection reaches that fetch. Token lifetimes allow five minutes of clock skew.

## Audit Logging

If you register an `IAuthAuditLogger` (see "Security Audit Logging" in the EasyReasy.Auth README), the sign-in endpoint invokes `OnExternalAuthAsync` after every attempt, success or failure, before the response is written. The begin endpoint reports nothing.

- `Provider` is `"oidc"`.
- `AttemptedSubject` is the identity as JSON, `{"issuer":"…","subject":"…"}`, on success and whenever a genuine token was refused by the flow's rules or by your handler returning null; null when the state, the cookie, the caller binding, the response, the provider, the code or the token failed.
- `FailureReason`, the audit category in the tables below, is `InvalidToken` for a failure of those, and `Rejected` for a genuine identity refused by the flow's rules or by your handler returning null.
- `FailureDetail` is the `OpenIdConnectAuthFailureReason` member name for every refusal the package makes itself, and null when your handler refused. Member names are a stable contract, so they are safe to store and query.

### Refusal reasons

| `OpenIdConnectAuthFailureReason` | Audit category | When |
|----------------------------------|----------------|------|
| `InvalidState` | `InvalidToken` | The state is missing, not this server's, tampered with or unreadable. |
| `ExpiredState` | `InvalidToken` | The state is older than `StateLifetime`. |
| `BindingCookieMissing` | `InvalidToken` | The browser did not send the flow's binding cookie: another browser, a completed flow, or one cleared by beginning too many. |
| `BindingCookieMismatch` | `InvalidToken` | The browser sent a different value for the flow's binding cookie. |
| `CallerBindingMismatch` | `InvalidToken` | The flow was begun with another caller binding than the completing endpoint expects. Refused before redemption. |
| `RedirectUriMismatch` | `InvalidToken` | The response arrived at another redirect URI than the flow named. |
| `IssuerParameterMismatch` | `InvalidToken` | The `iss` parameter is not the flow's provider's issuer. |
| `IssuerParameterMissing` | `InvalidToken` | The provider says it sends `iss`, and the response has none. |
| `UnknownProvider` | `InvalidToken` | The flow's provider key no longer resolves. |
| `ProviderChanged` | `InvalidToken` | The provider resolves to another issuer or client id than when the flow began. |
| `CodeRedemptionFailed` | `InvalidToken` | The code is missing, or the token endpoint refused it with `invalid_grant` or `invalid_request` (used, expired, or issued for another redirect URI or verifier), or answered without an ID token. |
| `InvalidToken` | `InvalidToken` | The ID token is malformed, not yet valid, has no `exp` or `sub`, or an `auth_time` that is not a number. |
| `InvalidSignature` | `InvalidToken` | The signature does not verify against the provider's keys, or the algorithm is not one of the provider's. |
| `InvalidAudience` | `InvalidToken` | The audience does not hold the client id, or `azp` is another client. |
| `InvalidIssuer` | `InvalidToken` | The issuer is not the provider's. |
| `TokenExpired` | `InvalidToken` | The ID token has expired. |
| `NonceMismatch` | `InvalidToken` | The nonce is missing or not the flow's. |
| `IdentityMismatch` | `Rejected` | The flow expected another account. |
| `MissingAcr` / `AcrNotAccepted` | `Rejected` | The flow named `acr` values, and the token has none or another. |
| `MissingAmr` / `AmrNotAccepted` | `Rejected` | The flow named `amr` values, and the token has none or none of them. |
| `MissingRequiredClaim` / `RequiredClaimMismatch` | `Rejected` | The flow required a claim value, and the token lacks the claim or has another value. |
| `MissingAuthTime` / `StaleAuthTime` | `Rejected` | The flow set a max age, and the token has no `auth_time` or an older one. |

When a token breaks several of the flow's rules, the first in this order is reported: identity, `acr`, `amr`, required claims, max age.

## Models

### `OpenIdConnectUserInfo`

| Property | Type | Description |
|----------|------|-------------|
| `ProviderKey` | `string` | The key of the provider the person signed in with. |
| `Identity` | `OpenIdConnectIdentity` | The issuer and `sub`. Link accounts by this. |
| `AuthenticationTime` | `DateTimeOffset?` | When the person last authenticated (`auth_time`), or null. |
| `Nonce` | `string` | The flow's nonce. |
| `AuthenticationContextClass` | `string?` | `acr`, as the provider asserts it. |
| `AuthenticationMethods` | `IReadOnlyList<string>` | `amr` as given, possibly empty. |
| `Email` | `string?` | For display and contact only, never an identifier. |
| `EmailVerified` | `bool` | Whether the provider says it verified `Email`. Informational. |
| `Name` | `string?` | The display name. |
| `Claims` | `IReadOnlyList<Claim>` | Every claim in the token, array claims flattened. `GetClaimValues(type)` returns one claim's values and `FindClaimValue(type)` its first, or null. |

### `OpenIdConnectIdentity`

A readonly struct of `Issuer` and `Subject`, compared ordinally. `ToString()` is the JSON form the audit record carries.

### `OpenIdConnectSignInSettings`

| Property | Type | Description |
|----------|------|-------------|
| `RedirectUri` | `string` (required) | The sign-in page's redirect URI, one of the provider's. |
| `Prompt` | `OpenIdConnectPrompt?` | `Login`, `None`, `Consent` or `SelectAccount`, or null to send none. |
| `MaxAuthenticationAge` | `TimeSpan?` | See "Re-checking a sign-in". |
| `AcceptedAcrValues` | `IReadOnlyList<string>?` | See "Re-checking a sign-in". |
| `AcceptedAmrValues` | `IReadOnlyList<string>?` | See "Re-checking a sign-in". |
| `RequiredClaimValues` | `IReadOnlyDictionary<string, string>?` | See "Re-checking a sign-in". |

### `OpenIdConnectAuthBeginRequest`

The input to `IOpenIdConnectAuthService.BeginAsync` for your own flows, which returns an `OpenIdConnectAuthBeginResult` with the `AuthorizationUrl` to send the browser to and the `State` in it. `BeginAsync` throws one `ArgumentException` listing every rule below the request breaks, and sets no cookie when it does.

| Property | Type | Description |
|----------|------|-------------|
| `ProviderKey` | `string` (required) | The provider's key. One no provider has makes `BeginAsync` return null. |
| `RedirectUri` | `string` (required) | One of the provider's redirect URIs, compared exactly. |
| `CallerBinding` | `string` (required) | What the flow is for; your completing endpoint passes the value it expects to `CompleteAsync`. Must not be null; it travels in the encrypted state, so keep it short. |
| `Prompt` | `OpenIdConnectPrompt?` | As in the settings. Must be a defined member. |
| `Nonce` | `string?` | Your nonce, or null for a random one. Must be null or non-blank. |
| `LoginHint` | `string?` | Fills in the provider's account picker; proves nothing. |
| `MaxAuthenticationAge` | `TimeSpan?` | At least one second; sent in whole seconds. |
| `AcceptedAcrValues` | `IReadOnlyList<string>?` | Null for no rule; otherwise at least one value and no blank one, so `[]` is refused rather than read as none. |
| `AcceptedAmrValues` | `IReadOnlyList<string>?` | As `AcceptedAcrValues`. |
| `RequiredClaimValues` | `IReadOnlyDictionary<string, string>?` | Non-blank names and values; no `auth_time`, `acr` or `amr`. |
| `ExpectedIdentity` | `OpenIdConnectIdentity?` | The account the person must sign in as, or null. Its issuer must be one the provider can issue as. |

### `OpenIdConnectAuthorizationResponse`

`{ code, state, iss, redirectUri }`: what the provider's redirect brought back, and the page's own address without its query. The sign-in endpoint's body and `CompleteAsync`'s input. `ToString()` redacts the code and state.

### `OpenIdConnectAuthCompletionResult`

| Property | Type | Description |
|----------|------|-------------|
| `Success` | `bool` | Whether every check passed. |
| `UserInfo` | `OpenIdConnectUserInfo?` | The verified identity, on success only. |
| `IdToken` | `string?` | The validated raw ID token, as signing evidence. On success only; never log it. |
| `IdTokenSigningKey` | `string?` | The provider's public key the token verified with, as JWK JSON. On success only. |
| `FailureReason` | `OpenIdConnectAuthFailureReason?` | Why the flow was refused, on failure only. |
| `AttemptedIdentity` | `OpenIdConnectIdentity?` | The account: on success and when a genuine token was refused by the flow's rules. |

### Endpoint bodies

The begin endpoint takes `OpenIdConnectSignInBeginRequest` (`{ providerKey, loginHint }`) and returns `OpenIdConnectSignInBeginResponse` (`{ authorizationUrl }`). The sign-in endpoint takes `OpenIdConnectAuthorizationResponse`. They follow the standard EasyReasy serialization pattern: `ToJson()`, `FromJson(string)`, `ToString()`.

## Building a provider package on the flow

`OpenIdConnectCodeFlow` is the engine behind `IOpenIdConnectAuthService`, public so a provider package can run the same flow with rules of its own on top: EasyReasy.Auth.Microsoft builds one under its own state key, resolves Entra's tenant segments as providers, and maps the flow's outcome to its own result. Its `CompleteAsync` returns an `OpenIdConnectCodeFlowResult` whose `UserInfo` is set whenever the token itself validated, even when a flow rule then refused it, so the package can apply its own rules first. A package's begin endpoint drops an unusable hint from the browser with `OpenIdConnectLoginHint.FromBrowser`, as this package's does. A package passes the flow's `OpenIdConnectAuthFailureReason` through rather than copying it, and files it in an audit record with `ToExternalAuthFailureReason()`, the one category rule the package's own sign-in endpoint uses. An application uses `IOpenIdConnectAuthService`.
