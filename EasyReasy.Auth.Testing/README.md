# EasyReasy.Auth.Testing

Test helpers for apps built on EasyReasy.Auth:

- **A WebAuthn authenticator in software**, for testing passkey and security-key endpoints against the real `WebAuthnVerifier`. It reads the options your endpoint returned, holds a real key pair, and answers with the body the browser's `PublicKeyCredential.toJSON()` posts, so a test drives your endpoints the way a browser and an authenticator would.
- **An OAuth test client and an in-memory store**, for driving your app's OAuth 2.1 authorization server end to end the way an MCP client and your consent page do ([below](#the-oauth-authorization-server)).

> **For test projects only.** The authenticator signs ceremonies with a private key it holds in memory, and the store keeps nothing past the process. Never reference this package from production code.

EasyReasy.Auth's own test suite uses both as its fakes, so the helpers your tests use are the ones the library's suite proves.

## Install

Add it to the test project, not the application:

```bash
dotnet add MyApp.Tests package EasyReasy.Auth.Testing
```

It uses only EasyReasy.Auth's public API and requires the EasyReasy.Auth version it was released with, or later; the two are released together.

## An endpoint test

Register a passkey, sign in with it, and check that the stored sign count moved. The endpoint paths and the `CredentialStore` below stand in for your application's own:

```csharp
[TestMethod]
public async Task Passkey_RegisterThenSignIn_StoresTheAdvancedSignCount()
{
    using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
    authenticator.SignCount = 1; // a counting authenticator; leave it at 0 for one that keeps no counter

    // Registration: the endpoint returns the creation options, the authenticator answers them.
    string creationOptions = await client.GetStringAsync("/api/passkeys/register/options");
    WebAuthnTestRegistration registration = authenticator.Register(creationOptions, "https://app.example");
    HttpResponseMessage registered = await client.PostAsync(
        "/api/passkeys/register",
        new StringContent(registration.Json, Encoding.UTF8, "application/json"));
    Assert.AreEqual(HttpStatusCode.OK, registered.StatusCode);

    // Sign-in: the same, with the request options.
    string requestOptions = await client.GetStringAsync("/api/passkeys/sign-in/options");
    WebAuthnTestAssertion assertion = authenticator.Assert(requestOptions, "https://app.example");
    HttpResponseMessage signedIn = await client.PostAsync(
        "/api/passkeys/sign-in",
        new StringContent(assertion.Json, Encoding.UTF8, "application/json"));
    Assert.AreEqual(HttpStatusCode.OK, signedIn.StatusCode);

    Assert.AreEqual(2u, authenticator.SignCount);
    Assert.AreEqual(2u, credentialStore.Find(authenticator.CredentialId).SignCount);
}
```

The origin is the page's origin, which must be one your `WebAuthnRelyingParty` allows. `Register` and `Assert` also take the typed `PublicKeyCredentialCreationOptions` and `PublicKeyCredentialRequestOptions`, for a test that calls `WebAuthnOptionsGenerator` and `WebAuthnVerifier` directly; `registration.Response` and `assertion.Response` are the typed `WebAuthnRegistrationResponse` and `WebAuthnAuthenticationResponse` for that.

## What it does

- **One credential per instance.** `Register` creates it; a second `Register` throws `InvalidOperationException`, and so does `Assert` before any `Register`. Make another instance for another credential.
- **ES256 by default**, RS256 with `Create(CoseAlgorithm.Rs256)`. The credential id is 16 random bytes unless you pass one of 1 to 1023 bytes.
- **Attestation `none`**, reporting `Transports` (`internal` by default) and the test AAGUID `fa4efa4e-fa4e-4fa4-bfa4-efa4efa4efa4`, which reads as fake at a glance.
- **The user handle** is recorded from the creation options' `user.id`, and every assertion answers with it.
- **The sign count** starts at 0: an authenticator that keeps no counter, like most platform passkeys. Once you set it above 0, each `Assert` raises it by one before signing.
- **Options without an `rpId`** use the origin's host, as a browser does.
- **The origin is written as a browser writes it**, `scheme://host[:port]` in lower case with the host in punycode and a default port left out, so `https://Example.com:443/` is written as `https://example.com`.

### Refusals

It throws `InvalidOperationException` saying why in these cases, where a real authenticator or browser refuses. These are the checks it makes, not every check a browser makes:

| Call | Refuses when |
|------|--------------|
| `Register`, `Assert` | the options' relying-party id is neither the origin's host nor a parent domain of it |
| `Register` | `excludeCredentials` names its credential |
| `Register` | `pubKeyCredParams` does not offer its algorithm |
| `Assert` | a non-empty `allowCredentials` does not name its credential |
| `Assert` | `rpId` is not the one it registered with |

A refused call changes nothing: no credential is created and the counter does not move.

### Bad input

Options a browser could not read throw `ArgumentException` naming the field: JSON that is not an object, a missing `challenge`, `rp`, `user.id` or `pubKeyCredParams`, a value that is not base64url, or a member of the wrong shape. So does an origin that is not an `http` or `https` origin with nothing after the host, such as `https://app.example/login`. `Create` throws `ArgumentException` for a credential id that is empty or longer than 1023 bytes, and `Register`, `Assert` and `Sign` on a disposed authenticator throw `ObjectDisposedException`.

## Testing your server's enforcement

Each setting makes the authenticator report what a dishonest or unusual one would, so a test can show your endpoint turns it away:

| Set | Verifier failure |
|-----|------------------|
| `VerifiesUser = false` with user verification required | `UserNotVerified` |
| `UserHandle = otherUsersHandle` on a passkey sign-in | `UserHandleMismatch` |
| `UserHandle = null` on a passkey sign-in | `UserHandleMissing` |
| `SignCount` lower than the stored count | `SignCounterRegressed` |

User verification is never refused: with `VerifiesUser` false the authenticator answers without the UV flag even when the options require it, which is exactly the client your server must reject. `BackupEligible` and `BackedUp` set the backup flags the server stores and reports.

## Tampered ceremonies

For a ceremony wrong in a way the settings above do not cover, build it yourself from:

- `CredentialId`, the credential id bytes
- `Algorithm`, the COSE algorithm it signs with
- `CosePublicKey`, the CBOR-encoded COSE public key it enrolls
- `Sign(ReadOnlySpan<byte>)`, a signature with its private key: an ASN.1 DER sequence for ES256, PKCS#1 v1.5 for RS256

The raw EC or RSA parameters are not exposed. A test that needs them reads them out of `CosePublicKey` in its own helper: for ES256 the x and y coordinates are the byte strings under labels -2 and -3, for RS256 the modulus and exponent under -1 and -2.

Only the `none` attestation format is written.

## The OAuth authorization server

`OAuthTestClient` drives your app's OAuth authorization server (EasyReasy.Auth section 17) the way an MCP client and your consent page do, over your test host's `HttpClient`. `InMemoryOAuthServerStore` stands in for your `IOAuthServerStore`. Your grant handler is the real one, so a test shows what it decides.

```csharp
[TestMethod]
public async Task Connect_AgentForAPerson_CarriesTheOrganization()
{
    // Program.cs registered AddEasyReasyOAuthServer<MyOAuthStore, MyGrantHandler> and opened /api/projects to
    // connections with .AllowOAuthClients(); the test host replaces the store:
    //   services.AddSingleton<IOAuthServerStore>(new InMemoryOAuthServerStore());
    OAuthTestClient oauth = new OAuthTestClient(client);     // a TestServer client: it does not follow redirects

    OAuthTestRegisteredClient agent = await oauth.RegisterAsync("https://claude.ai/api/mcp/auth_callback", clientName: "Claude");
    string personToken = await SignInAsync("ada@lab.example");   // your app's own sign-in

    OAuthTestTokens tokens = await oauth.ConnectAsync(agent, personToken, new { organizationId = 7 }, resource: "https://app.example/mcp");

    HttpRequestMessage call = new HttpRequestMessage(HttpMethod.Get, "/api/projects");
    call.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    Assert.AreEqual(HttpStatusCode.OK, (await client.SendAsync(call)).StatusCode);

    OAuthTestTokens refreshed = await oauth.RefreshAsync(agent, tokens.RefreshToken);
    Assert.AreNotEqual(tokens.RefreshToken, refreshed.RefreshToken);
}
```

`ConnectAsync` is the three middle steps below in one call. Take them one at a time to look between them or to test a refusal:

| Step | Does | Returns |
|------|------|---------|
| `RegisterAsync(redirectUri, method, name)` | `POST /oauth/register`; public by default, as MCP clients register | `OAuthTestRegisteredClient` |
| `AuthorizeAsync(client, resource, scope, redirectUri)` | `GET /oauth/authorize` with a fresh PKCE verifier and state; reads the redirect to your consent page | `OAuthTestAuthorization` with the request id and the redirect URI it named |
| `GetRequestAsync(authorization, accessToken)` | what your consent page reads | `OAuthAuthorizationRequestDetails` |
| `ApproveAsync(authorization, accessToken, body)` | approves as the signed-in person with your approval body; checks the redirect's `state` and `iss` | the code |
| `DenyAsync(authorization, accessToken)` | denies | the redirect's `error`, `access_denied` |
| `RedeemCodeAsync(authorization, code, codeVerifier)` | `POST /oauth/token` with `authorization_code` | `OAuthTestTokens` |
| `RefreshAsync(client, refreshToken)` | `POST /oauth/token` with `refresh_token` | `OAuthTestTokens` |
| `RevokeAsync(client, token)` | `POST /oauth/revoke` | |

A client authenticates the way it registered: by `client_id` alone, a Basic header or the form body. For a client identified by a metadata document, or one your test stored directly, build the `OAuthTestRegisteredClient` yourself.

**Refusals.** Any answer other than a successful step's throws `OAuthTestFlowException` with the `StatusCode`, the OAuth `Error` (`invalid_grant`, `invalid_client`, `access_denied`...) and the `ErrorDescription`, which for a 403 from your handler's `Refuse(detail)` is that detail:

```csharp
OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
    () => oauth.ApproveAsync(authorization, personToken, new { organizationId = organizationWithConnectionsOff }));
Assert.AreEqual(HttpStatusCode.Forbidden, refused.StatusCode);
```

To show your server refuses something a client got wrong, change one value. `AuthorizeAsync`'s `redirectUri` names another redirect URI (a loopback one on another port is accepted, and the approval and redemption then use it), and `RedeemCodeAsync`'s `codeVerifier` sends another verifier. `OAuthTestRegisteredClient` and `OAuthTestAuthorization` are records, so the rest is `with`: `client with { ClientSecret = "wrong" }`, `authorization with { Resource = "https://other.example/mcp" }`. `OAuthTestPkce` makes a verifier and its S256 challenge, and `OAuthTestClient.ParseQuery` reads a redirect's parameters, for a request you build by hand.

**The store.** `InMemoryOAuthServerStore` is thread-safe, atomic where the interface requires, and returns copies, as a database would. `Clients`, `AuthorizationRequests`, `AuthorizationCodes` and `RefreshTokens` list what it holds, for assertions such as "the grant's refresh tokens are all invalidated". `DeleteExpiredAsync` deletes by the contract's rule, so a test can call it with a clock of its own and see what the server's sweep would leave.
