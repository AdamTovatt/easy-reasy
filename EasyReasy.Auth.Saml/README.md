# EasyReasy.Auth.Saml

SAML 2.0 sign-in for the EasyReasy.Auth JWT pipeline, for organizations whose identity provider speaks SAML. Your application is the service provider. It sends the browser to the identity provider with an AuthnRequest, receives the signed response at its assertion consumer service, and issues the same JWTs (and optional refresh tokens) as for any other sign-in.

XML parsing, signature verification and decryption are done by [ITfoxtec.Identity.Saml2](https://github.com/ITfoxtec/ITfoxtec.Identity.Saml2). On top of that, the package checks every response itself, whatever the library also checks. The checks are listed under [What a response must pass](#what-a-response-must-pass).

The same service also lets your application link an account, or re-check who someone is at a sensitive moment such as signing a record.

The package never creates users. Your handler links a verified `SamlIdentity` to one of your users: the identity provider's key, its entity id and the subject it asserted, never an email address. See [Identity](#identity).

> Versions `1.0.0-preview.N` have not had an independent security review.

## How a sign-in runs

1. **Begin.** The browser asks your application to sign in with an identity provider. The package builds an AuthnRequest and keeps the pending sign-in in your store, sealed. It sets a short-lived `__Host-` binding cookie and returns the identity provider's URL. Only the HTTP-Redirect binding is used, signed in the query string when you configure a signing key.
2. **Assertion consumer service.** The identity provider posts the response to `POST /api/auth/saml/acs`, a cross-site form post.
   - The package consumes the pending request that the response's `InResponseTo` names, at most once.
   - It checks the response against the identity provider that request named, then applies the sign-in's rules.
   - It keeps the outcome under a random single-use code and redirects (303) to your completion page with `?code=`.
3. **Completion.** Your completion page posts the code as JSON, same-site. The `SameSite=Lax` binding cookie comes with it, which it never does on the identity provider's cross-site post. The package redeems the code once, checks and clears the cookie, and returns the outcome.

The browser that completes must be the browser that began. Begin sets a `__Host-easyreasy-saml-` binding cookie (HttpOnly, Secure, SameSite=Lax) for each sign-in, through EasyReasy.Auth's `FlowBindingCookie`. One browser holds at most 5 of them, counted for SAML sign-ins alone, apart from OpenID Connect's: a sixth SAML sign-in begun while 5 are unfinished clears the 5, and those sign-ins then fail as `BindingCookieMissing`. A response that answers no request this server began, or one already answered, is refused. That covers unsolicited, replayed and IdP-initiated responses.

## Quick start

### 1. Install

```bash
dotnet add package EasyReasy.Auth.Saml --prerelease
```

### 2. Register

```csharp
builder.Services.AddEasyReasyAuth(jwtSecret);
builder.Services.AddEasyReasySamlAuth(new SamlAuthOptions
{
    ServiceProviderEntityId = "https://app.example/saml",
    AssertionConsumerServiceUrl = new Uri("https://app.example/api/auth/saml/acs"),
    StateKey = Convert.FromBase64String(builder.Configuration["Saml:StateKey"]!), // 32 bytes, used for nothing else
    // Optional:
    // SigningCertificate = ...,       // signs AuthnRequests; required by an identity provider that wants them signed
    // DecryptionCertificates = [...], // decrypts assertions; when set, a plaintext assertion is refused
    // RequestLifetime = TimeSpan.FromMinutes(10),   // how long the person has at the identity provider, a second factor included
    // CompletionLifetime = TimeSpan.FromMinutes(2), // how long the completion page has to redeem its code
    // SweepInterval = TimeSpan.FromHours(1),        // how often expired rows are deleted
});
builder.Services.AddScoped<ISamlIdentityProviderResolver, MyIdentityProviderResolver>();
builder.Services.AddScoped<ISamlAuthStore, MySamlAuthStore>();
builder.Services.AddScoped<ISamlAuthHandler, MySamlAuthHandler>();

WebApplication app = builder.Build();
app.UseEasyReasyAuth();
SamlAuthEndpoints samlEndpoints = app.AddSamlAuthEndpoints();
samlEndpoints.AssertionConsumerService.RequireRateLimiting("saml-acs"); // a policy you define with AddRateLimiter
```

The application has one service provider entity id and one assertion consumer service URL. `AssertionConsumerServiceUrl` is `SamlAuthPaths.AssertionConsumerService` (`/api/auth/saml/acs`) as the browser reaches it.

`AddSamlAuthEndpoints` returns one builder per endpoint, so you can add a rate limit or other metadata to one of them. Rate-limit the assertion consumer service at least: anyone can post to it, and it parses the XML before it can tell whether the response answers a request this server began.

### 3. Resolve identity providers

Identity providers are resolved per request by a stable key you choose, such as a customer's id. A provider can then be added, or its metadata refreshed, without a restart. The key is part of every identity the provider yields, so never reuse a key for another identity provider or customer, and never change the key of one in use.

```csharp
public class MyIdentityProviderResolver : ISamlIdentityProviderResolver
{
    private readonly ICustomerRepository _customers;
    private readonly SamlIdentityProviderFactory _identityProviders;

    public MyIdentityProviderResolver(ICustomerRepository customers, SamlIdentityProviderFactory identityProviders)
    {
        _customers = customers;
        _identityProviders = identityProviders;
    }

    public async Task<SamlIdentityProvider?> FindIdentityProviderAsync(string identityProviderKey, CancellationToken cancellationToken)
    {
        Customer? customer = await _customers.FindBySamlKeyAsync(identityProviderKey, cancellationToken);
        if (customer?.SamlMetadataXml == null)
        {
            return null;
        }

        // An attribute that never changes for the person, when the IdP cannot send a persistent NameID:
        // SamlIdentitySource identitySource = SamlIdentitySource.Attribute("http://schemas.example/employeeid");
        return _identityProviders.Create(customer.SamlMetadataXml);
    }
}
```

**Supplying the metadata**
- `SamlIdentityProviderFactory`, which `AddEasyReasySamlAuth` registers as a singleton, builds a `SamlIdentityProvider` from the identity provider's metadata XML. **You supply and refresh the XML**: the package never fetches it.
- The factory reads one `md:EntityDescriptor` from it (an aggregate is refused): its entity id, its HTTP-Redirect single sign-on URL and its signing certificates.
- It keeps the metadata parsed per distinct XML, so building the provider on every call is cheap, an unchanged configuration is not parsed again, and a changed one takes effect at once.
- Metadata it cannot use makes `Create` throw one `ArgumentException` listing every problem, so every `SamlIdentityProvider` can be signed in with. An exception from your resolver fails the request it serves with a 500, so check metadata when you accept it, to report the problems to whoever supplied it:

```csharp
IReadOnlyList<string> problems = SamlIdentityProvider.FindMetadataProblems(uploadedMetadataXml);
```

**Trusting the metadata**
- The signing certificates in the metadata are the pins. Several are accepted at once, so the identity provider can roll its key over by publishing the new certificate beside the old one.
- Whoever controls the metadata controls whose assertions you accept. Take it from the identity provider's administrator over a channel you trust, or check its signature before storing it.
- The entity id in the metadata is only text. Two keys whose metadata claim the same entity id are still two identity providers, and an account linked through one never matches a sign-in through the other.

### 4. Implement the store

The store keeps the sign-ins in progress, in one table. Every record holds only its kind, a hash, a value sealed with `StateKey`, and an expiry. Your database never holds an identity, a rule or a caller binding in plaintext. Store `SamlAuthRecordKind` as its number, which never changes.

`ConsumeAsync` must be atomic: it removes the record and returns its sealed value to the first caller only. That is what makes a response and a completion code usable once.

```sql
CREATE TABLE saml_auth_records (
    kind SMALLINT NOT NULL,
    hash TEXT NOT NULL,
    sealed_value TEXT NOT NULL,
    expires_at TIMESTAMP NOT NULL,
    PRIMARY KEY (kind, hash));

-- StoreAsync
INSERT INTO saml_auth_records (kind, hash, sealed_value, expires_at) VALUES (@kind, @hash, @sealed_value, @expires_at);
-- ConsumeAsync
DELETE FROM saml_auth_records WHERE kind = @kind AND hash = @hash AND expires_at > @now RETURNING sealed_value;
-- DeleteExpiredAsync
DELETE FROM saml_auth_records WHERE expires_at <= @now;
```

The package deletes expired rows through `DeleteExpiredAsync`, every `SweepInterval` (an hour by default).

### 5. Implement the handler

```csharp
public class MySamlAuthHandler : ISamlAuthHandler
{
    private readonly IUserRepository _users;

    public MySamlAuthHandler(IUserRepository users)
    {
        _users = users;
    }

    public Task<SamlSignInSettings?> GetSignInSettingsAsync(string identityProviderKey, HttpContext httpContext)
    {
        // The key is the browser's: return null for one you do not offer for sign-in, such as a provider kept only for
        // linking accounts, and the begin endpoint answers 400.
        return Task.FromResult<SamlSignInSettings?>(new SamlSignInSettings { CompletionPath = "/auth/saml/done" });
    }

    public async Task<AuthResponse?> HandleSamlUserAsync(
        SamlUserInfo userInfo,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService? refreshTokenService,
        HttpContext httpContext)
    {
        // Link by all three parts of the identity, never by an email attribute.
        SamlIdentity identity = userInfo.Identity;
        User? user = await _users.FindBySamlIdentityAsync(identity.IdentityProviderKey, identity.IdentityProviderEntityId, identity.Subject);
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

### 6. The frontend

```javascript
// Sign-in button
const begin = await fetch("/api/auth/saml/begin", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ identityProviderKey: "acme" }),
});
const { redirectUrl } = await begin.json();
window.location.assign(redirectUrl);

// The completion page, /auth/saml/done
const code = new URLSearchParams(window.location.search).get("code");
const signIn = await fetch("/api/auth/saml", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ code }),
});
const authResponse = await signIn.json(); // the same AuthResponse as the other sign-in endpoints
```

### 7. Give the identity provider your metadata

Build the options once and pass the same instance to `AddEasyReasySamlAuth` and to `Create`:

```csharp
SamlAuthOptions samlOptions = new SamlAuthOptions { /* as in step 2 */ };
builder.Services.AddEasyReasySamlAuth(samlOptions);

string spMetadataXml = SamlServiceProviderMetadata.Create(samlOptions);
```

The metadata names your entity id and assertion consumer service (HTTP-POST). It asks for signed assertions and a persistent NameID, and carries only the certificates of your signing and decryption keys. Serving it, at a URL or as a download, is yours.

## Endpoints

| Endpoint | Body | Answer |
|---|---|---|
| `POST /api/auth/saml/begin` | `{ "identityProviderKey": "…" }` | 200 `{ "redirectUrl": "…" }`, or 400 when your handler returns no settings for the key or it resolves to no identity provider |
| `POST /api/auth/saml/acs` | the identity provider's form post (`SAMLResponse`) | 303 to the sign-in's completion path with `?code=`; 400 when the post is not a form or has no `SAMLResponse`, when that is not base64 XML or names no `InResponseTo`, or when it answers no pending request (unknown, already answered or expired) |
| `POST /api/auth/saml` | `{ "code": "…" }` | 200 with the `AuthResponse`, or 401 |

- All three are anonymous and `no-store`. Their paths are constants on `SamlAuthPaths`.
- The begin endpoint fails with a 500 when the identity provider wants signed requests and `SigningCertificate` is not set, or when your handler's settings are ones `BeginAsync` refuses: both are configuration errors. Any endpoint fails with a 500 when your resolver, store or handler throws. None of these is a refusal, and none is audited.
- The assertion consumer service opts out of antiforgery, because it is a cross-site post by design. What stands in for it is the response answering a request this server began, once, completed by the browser that began it.
- The browser chooses only the identity provider. The completion page and the rules come from your handler.
- The sign-in endpoint is audited through `IAuthAuditLogger.OnExternalAuthAsync`:
  - The provider is `saml`.
  - The `SamlAuthFailureReason` name is the failure detail.
  - The account is the attempted subject, as `SamlIdentity.ToString()`.

## Your own flows: linking an account, re-checking at a sensitive moment

Inject `ISamlAuthService`, which is scoped, into your own authenticated endpoints:

```csharp
SamlAuthBeginResult? begun = await samlAuth.BeginAsync(new SamlAuthBeginRequest
{
    IdentityProviderKey = user.SamlKey,
    CallerBinding = $"sign:{user.Id}:{record.Id}",
    CompletionPath = "/records/sign/saml-done",
    ForceAuthentication = true,
    MaxAuthenticationAge = TimeSpan.FromMinutes(5),
    AcceptedAuthnContextClassRefs = ["http://schemas.microsoft.com/claims/multipleauthn"],
    ExpectedIdentity = user.SamlIdentity,
}, httpContext);
// null when the key resolves to no identity provider

// The completion page posts the code to your endpoint, which calls:
SamlAuthCompletionResult result = await samlAuth.CompleteAsync(code, $"sign:{user.Id}:{record.Id}", httpContext);
if (result.Success)
{
    // result.UserInfo: the verified identity, AuthenticationInstant, AuthnContextClassRef, attributes
}
```

**The rules a begin request can set**
- `ForceAuthentication` sends `ForceAuthn`.
- `MaxAuthenticationAge` refuses an `AuthnInstant` older than that.
- `AcceptedAuthnContextClassRefs` is sent as an exact `RequestedAuthnContext`. It refuses an assertion with no class or with another class. A missing value is refused, never assumed.
- `ExpectedIdentity` refuses any other account. Its key must be the request's `IdentityProviderKey`, or Begin throws.
- `CompletionPath` is a path on your site: one leading `/`, no `//`, no backslash and no `#`. It may carry a query string; the code joins it.

`CompleteAsync` takes the caller binding your endpoint began with and refuses a code begun with any other as `CallerBindingMismatch`. So a re-check endpoint never accepts a plain sign-in, which carried none of its rules, and the sign-in endpoint never accepts a code from your own flows.

**These are the identity provider's assertions.** Whether an identity provider honours `ForceAuthn`, and whether a class it reports proves that a second factor ran for this act, is up to the identity provider. The package checks neither, for any provider. Choose classes your identity provider documents, and test that it refuses what it cannot meet.

## Identity

A `SamlIdentity` has three parts: the key your resolver resolved the identity provider by, its entity id, and the subject it asserted. Store all three as the account link. It is compared ordinally, and its `ToString()` is JSON.

The key is part of it because the key's configuration is what the response was verified against: its metadata pins the signing certificates. An entity id proves nothing on its own. Another customer's administrator could put your customer's entity id in their own metadata, sign assertions with their own key, and name your customer's users. Those sign-ins carry the other key, so they never match your customer's accounts.

The subject comes from the provider's `SamlIdentitySource`:

- **`SamlIdentitySource.PersistentNameId`** (the default).
  - The NameID must have the persistent format.
  - Its `NameQualifier` and `SPNameQualifier`, when present, must name the identity provider and this service provider. They are checked, not stored, so an identity provider that starts or stops sending them does not change the account.
  - Transient, email and unspecified NameIDs are refused.
- **`SamlIdentitySource.Attribute(name)`**.
  - One attribute whose value never changes for the person replaces the NameID.
  - A missing or multi-valued attribute is refused.

An email address is never an identity. Attributes are in `SamlUserInfo.Attributes`, for display and the like.

## What a response must pass

ITfoxtec parses the response, refuses DTDs, decrypts it and verifies every signature against the pinned certificates. The package then requires each of the following itself, and a test in the package covers each one.

**The request it answers**
- It answers the pending request its `InResponseTo` names. That request is consumed atomically before anything else, and must not have expired.
- The response and the signed bearer confirmation both name that request.

**Who sent it, and to whom**
- The response's and the assertion's issuers equal the entity id of the identity provider the request named.
- The provider is re-resolved at the assertion consumer service, and its entity id must not have changed.
- `Destination` and the bearer `Recipient` equal your assertion consumer service URL.
- Every `AudienceRestriction` names your entity id.

**The signature**
- The assertion itself is signed. A signed response alone is not enough, and the identity is read only from the signed assertion.
- The signature uses RSA with SHA-256, SHA-384 or SHA-512, and exclusive canonicalization.
- The digest is SHA-256, SHA-384 or SHA-512.

**Encryption**
- When you configure decryption keys, a plaintext assertion is refused.
- Every encrypted part must use AES-GCM with an RSA-OAEP key transport, checked before anything is decrypted. RSA-OAEP is accepted under XML Encryption 1.0's identifier and 1.1's, which names its digest and mask function. A 1.1 key that names neither, leaving them to the specification's SHA-1 defaults, fails to decrypt and is refused, since ITfoxtec reads a missing mask function as MGF1-SHA256. AES-CBC and RSA PKCS#1 v1.5 are refused.

**Times and content**
- The status is Success.
- There is exactly one bearer confirmation, with no `NotBefore`.
- The assertion is within its conditions' and confirmation's times, with 2 minutes of clock skew.
- There is exactly one `AuthnStatement`.

The suite drives an in-process fake identity provider through hostile responses:
- signature wrapping in several forms
- unsigned, response-only-signed and tampered assertions
- unpinned keys
- SHA-1 signatures and digests
- DTDs, external entities and entity bombs
- comments inside signed values
- replay
- wrong audience, recipient, destination and identity provider
- expiry
- encryption downgrades

A separate CI job runs the real flow against Keycloak, in a container. It covers a signed AuthnRequest, Keycloak's login, a persistent NameID, a session reused and then forced again with `ForceAuthn`, a replayed response, an encrypted assertion, an attribute identity, and a realm signing with RSA-SHA1 being refused.

## Identity provider settings

The identity provider must sign the assertion with RSA and SHA-256, SHA-384 or SHA-512, with a digest from the same three and exclusive canonicalization. When it encrypts, it must use AES-GCM and RSA-OAEP, naming the digest when it uses XML Encryption 1.1's identifier. A response from settings that fail this still reaches the completion page with a code, since the assertion consumer service keeps the refusal under it. Completing the code gives `InvalidResponse` (401 from the sign-in endpoint), and the warning logged at the assertion consumer service names the check that failed in its exception.

In Keycloak, the SAML client settings the tests use are:

| Setting | Value |
|---|---|
| Client ID | your `ServiceProviderEntityId` |
| Assertion Consumer Service POST Binding URL, and Valid redirect URIs | your `AssertionConsumerServiceUrl` |
| Sign documents, Sign assertions | on |
| Signature algorithm | `RSA_SHA256` |
| SAML signature canonicalization method | Exclusive |
| Client signature required | on, with your `SigningCertificate`. Keycloak's metadata sets `WantAuthnRequestsSigned`, so the package requires a signing certificate for it |
| Force name ID format, Name ID format | on, `persistent` |
| Encrypt assertions (optional) | on, with a certificate from your `DecryptionCertificates`; encryption algorithm AES-256-GCM (`http://www.w3.org/2009/xmlenc11#aes256-gcm`), key algorithm XML Encryption 1.1's RSA-OAEP (`http://www.w3.org/2009/xmlenc11#rsa-oaep`), digest method SHA-256 (`http://www.w3.org/2001/04/xmlenc#sha256`) |

## Failure reasons

The category is the `ExternalAuthFailureReason` the sign-in endpoint audits a refusal under. `reason.ToExternalAuthFailureReason()` gives it, so an endpoint of your own on `ISamlAuthService` can audit the same way.

| `SamlAuthFailureReason` | Category | When |
|---|---|---|
| `UnknownCompletionCode` | `InvalidToken` | The code is unknown, already redeemed or expired |
| `BindingCookieMissing` | `InvalidToken` | The completing browser has no binding cookie for the sign-in: another browser, or one that began 5 more sign-ins since, which cleared it |
| `BindingCookieMismatch` | `InvalidToken` | Its binding cookie does not match |
| `UnknownIdentityProvider` | `InvalidToken` | The identity provider no longer resolves at the assertion consumer service |
| `IdentityProviderChanged` | `InvalidToken` | Its entity id changed after Begin |
| `InvalidResponse` | `InvalidToken` | The response failed a check above; the reason is logged as a warning |
| `CallerBindingMismatch` | `InvalidToken` | The code was begun with another caller binding than the completing endpoint passed: a code from your own flow posted to the sign-in endpoint, or a plain sign-in's code posted to your re-check |
| `MissingIdentity` | `Rejected` | No identity the identity source accepts |
| `IdentityMismatch` | `Rejected` | Another account than `ExpectedIdentity` |
| `StaleAuthentication` | `Rejected` | `AuthnInstant` older than `MaxAuthenticationAge` |
| `MissingAuthnContextClass` | `Rejected` | Classes are required and the assertion names none |
| `AuthnContextClassNotAccepted` | `Rejected` | The class is not one of the accepted ones |

## Not supported

- Single Logout.
- The artifact binding.
- IdP-initiated sign-in.
- AuthnRequests over HTTP-POST.
