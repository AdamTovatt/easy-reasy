# EasyReasy.Auth.Client

[← Back to EasyReasy System](../README.md)

[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.Auth.Client-blue.svg)](https://www.nuget.org/packages/EasyReasy.Auth.Client)

A lightweight .NET client library for authenticating with EasyReasy.Auth servers, designed for simplicity and automatic token management.

## Overview

EasyReasy.Auth.Client provides a simple HTTP client wrapper that automatically handles authentication with EasyReasy.Auth servers. It supports both API key and username/password authentication, with automatic token refresh and retry logic.

**Why Use EasyReasy.Auth.Client?**

- **Automatic authentication**: Handles JWT token acquisition and renewal transparently
- **Multiple auth methods**: Support for API key and username/password authentication
- **Token management**: Automatic token refresh before expiration (5-minute buffer)
- **Retry logic**: Automatically retries requests on 401 Unauthorized with fresh tokens
- **Simple API**: Drop-in replacement for HttpClient with minimal code changes
- **Flexible configuration**: Customizable auth endpoints and HTTP client settings

## Quick Start

### 1. Add to your project

Install via NuGet:
```sh
dotnet add package EasyReasy.Auth.Client
```

### 2. Create an authorized client

**Credentials must carry something.** Both credential constructors reject a `null` credential with `ArgumentNullException` and an empty one with `ArgumentException`: an empty credential carries nothing to authenticate with, so it is refused rather than sent. A whitespace-only credential is a value you supplied and is sent as given — the client does not decide what the server will accept.

#### API Key Authentication
```csharp
using (HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/"))
{
    AuthorizedHttpClient authorizedClient = new AuthorizedHttpClient(httpClient, "your-api-key-here");

    // The client will automatically authenticate on first use
    HttpResponseMessage response = await authorizedClient.GetAsync("api/data");
}
```

#### Username/Password Authentication
```csharp
using (HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/"))
{
    AuthorizedHttpClient authorizedClient = new AuthorizedHttpClient(
        httpClient, 
        username: "your-username", 
        password: "your-password");

    // The client will automatically authenticate on first use
    HttpResponseMessage response = await authorizedClient.GetAsync("api/data");
}
```

### 3. Use the client like a regular HttpClient

```csharp
using (HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/"))
{
    AuthorizedHttpClient authorizedClient = new AuthorizedHttpClient(httpClient, "your-api-key");

    // GET requests
    HttpResponseMessage response = await authorizedClient.GetAsync("api/users");

    // POST requests
    StringContent content = new StringContent("{\"name\":\"John\"}", Encoding.UTF8, "application/json");
    HttpResponseMessage response = await authorizedClient.PostAsync("api/users", content);

    // Custom requests
    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, "api/users/123");
    request.Content = new StringContent("{\"name\":\"Jane\"}", Encoding.UTF8, "application/json");
    HttpResponseMessage response = await authorizedClient.SendAsync(request);
}
```

## Advanced Usage

### Custom Auth Endpoints

By default, the client uses the standard EasyReasy.Auth endpoints, as paths relative to the client's base address:
- API Key: `api/auth/apikey`
- Username/Password: `api/auth/login`

Keep your own endpoints relative too. A leading slash makes the path absolute against the host, discarding any path prefix in the base address — `https://api.example.com/myapp` plus `/api/auth/login` resolves to `https://api.example.com/api/auth/login`.

You can customize these endpoints:

```csharp
using (HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/"))
{
    // Custom API key endpoint
    AuthorizedHttpClient apiKeyClient = new AuthorizedHttpClient(
        httpClient, 
        "your-api-key", 
        authEndpoint: "custom/auth/apikey");

    // Custom login endpoint
    AuthorizedHttpClient loginClient = new AuthorizedHttpClient(
        httpClient, 
        "username", 
        "password", 
        authEndpoint: "custom/auth/login");
}
```

### Manual Authentication Control

You can manually control when authentication happens:

```csharp
using (HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/"))
{
    AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, "api-key");

    // Force authentication now
    await client.EnsureAuthorizedAsync();

    // Check authentication type
    if (client.AuthenticationType == AuthorizedHttpClient.AuthType.ApiKey)
    {
        Console.WriteLine("Using API key authentication");
    }
}
```

### Handling Authorization Issues

Sometimes the server may reject a token even when the client thinks it's still valid. The client provides methods to handle these scenarios:

```csharp
using (HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/"))
{
    AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, "api-key");

    try
    {
        HttpResponseMessage response = await client.GetAsync("api/data");
        // Process response
    }
    catch (UnauthorizedAccessException)
    {
        // Force a fresh authorization attempt
        await client.ForceAuthorizeAsync();
        
        // Try the request again
        HttpResponseMessage response = await client.GetAsync("api/data");
    }
}
```

#### Force Authorization Methods

- **`ForceAuthorizeAsync()`**: Bypasses the authorization check and always performs a fresh authentication. Useful when the server rejects a token that the client thinks is still valid.

- **`ForceReauthorizeAsync()`**: Clears all current authorization state and performs a completely fresh authentication. This ensures no residual state interferes with the new authentication.

On a client constructed from an `AuthResponse`, `ForceAuthorizeAsync()` redeems the refresh token first, so it renews the session when the server accepts it. `ForceReauthorizeAsync()` clears the refresh token before authorizing, and such a client has no credentials to fall back on, so it always throws `SessionNotRenewableException` and spends a session that may still have been good. Use `ForceAuthorizeAsync()` there.

```csharp
// Force fresh authentication without clearing state
await client.ForceAuthorizeAsync();

// Clear all state and perform fresh authentication
await client.ForceReauthorizeAsync();
```

### When the Address Is Not an Auth Server

A base address that points at something other than an auth server does not fail as a connection error. A host
that answers the auth path with a redirect to its own sign-in page returns `200` carrying HTML — a success by
every check the client can make — so the body reaches the parser and fails there. So does a body that parses
but is not an auth response: no `token`, or an `expiresAt` that is not a date and time.

That case throws `InvalidAuthResponseException`, carrying what separates it from a genuinely corrupt response
from the right server:

| Property | What it carries |
|---|---|
| `RequestUri` | The URI the request ended at, **after any redirects**. This, not the configured base address, is the host that answered. `null` when the handler recorded none. |
| `StatusCode` | The status the endpoint answered with. Always a success status — an unsuccessful one is reported before the body is parsed. |
| `ContentType` | The response's content type, or `null` when it carried none. `text/html` here is the signature of a sign-in page. |
| `BodySnippet` | The start of the body, capped at `InvalidAuthResponseException.BodySnippetLength` characters, whitespace collapsed, with a trailing `…` when the body continued. Values under a name containing `token`, `secret` or `password` are replaced with `[REDACTED]`, whether the name is a JSON property or an HTML field — a body that nearly is an auth response carries a real token, and a sign-in page carries the hidden fields of one. |

The failure the parse reported is kept as the inner exception: an `ArgumentException` from
`AuthResponse.FromJson`, which itself carries the `JsonException` when the body failed to parse at all. A body
that parses but is not an auth response has no `JsonException` under it — what was wrong with it is in the
`ArgumentException`'s message.

The body is read under a cap of 64 K characters. An auth response is a token, an expiry and a refresh token, so
nothing near that size is one; a longer body is read only that far and reported as the body that is not an auth
response. The same cap and the same redaction apply to the body of an *unsuccessful* answer, which reaches the
message of the `UnauthorizedAccessException` or `HttpRequestException` reporting it.

Redaction reads the text rather than a parsed document, which is what lets it blank a token in a body no parser
will accept — one cut off mid-value, or JSON embedded in a page. The cost is an outer bound on what it can
recognise: an unquoted HTML attribute value, and a value whose name is out of reach behind an unescaped quote
earlier in a malformed body, are not blanked. Treat the snippet as a diagnosis, not as something safe to
forward anywhere a full body would not be.

```csharp
try
{
    await client.EnsureAuthorizedAsync();
}
catch (InvalidAuthResponseException exception)
{
    // A consumer can say the thing the library cannot know:
    if (exception.RequestUri?.Host == "example.com")
        Console.WriteLine("That domain serves the public site. The API is at https://app.example.com/.");
    else
        Console.WriteLine(exception.Message);
}
```

The same exception covers the refresh endpoint. An unsuccessful status there is a refusal to refresh, and the
client falls back as before — to full re-authentication when it holds credentials, and to a
`SessionNotRenewableException` when it was constructed pre-authorized and so has none. A `408`, `429` or `5xx`
is not a refusal. The refresh token is kept, a client with credentials still signs in, and a client without
throws `HttpRequestException` with that status; see [Persisting Auth State with
Callbacks](#persisting-auth-state-with-callbacks). A *success* carrying
something that is not an auth response is neither: that is the endpoint not being one, which re-authenticating
against the same address would only repeat, so it throws here instead of falling back.

### Persisting Auth State with Callbacks

All constructors accept an optional `onAuthResponseChanged` callback that fires whenever the auth state changes — on initial authentication, token refresh, or re-auth. This is useful for CLI tools and long-running processes that want to persist the token to disk:

```csharp
using (HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/"))
{
    // First launch: authenticate with API key and persist the token
    AuthorizedHttpClient client = new AuthorizedHttpClient(
        httpClient,
        "your-api-key",
        onAuthResponseChanged: authResponse =>
        {
            File.WriteAllText("auth-state.json", authResponse.ToJson());
        });

    await client.GetAsync("api/data");
}

// Subsequent launches: reuse the persisted token
string savedJson = File.ReadAllText("auth-state.json");
AuthResponse savedAuth = AuthResponse.FromJson(savedJson);

using (HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/"))
{
    AuthorizedHttpClient client = new AuthorizedHttpClient(
        httpClient,
        savedAuth,
        onAuthResponseChanged: authResponse =>
        {
            // Keeps the persisted state up to date on transparent token refreshes
            File.WriteAllText("auth-state.json", authResponse.ToJson());
        });

    await client.GetAsync("api/data");
}
```

`AuthResponse.FromJson` throws `ArgumentException` for JSON it cannot read as an auth response: not JSON at
all, the literal `null`, a `token` or `expiresAt` that is missing or written out as `null`, or an `expiresAt`
that is not an ISO 8601 date and time. A file truncated or overwritten by something else reads every one of
those ways. Handle it where the persisted state is loaded, and fall back to authenticating from credentials.

Reading the expiry is part of that check, so an instance that came from `FromJson` always has one: use
`AuthResponse.ExpirationTime` for the expiry as a `DateTime` rather than parsing `ExpiresAt` again. It is
always UTC, whatever offset the wire value carried — `ExpiresAt` parsed with a general date parser comes back
as a local time for an offset-bearing value, and comparing that against a UTC clock is hours wrong on a machine
that is not on UTC. Building an `AuthResponse` in code skips the check: `ExpirationTime` throws
`FormatException` for a value that is not an ISO 8601 date and time, and the pre-authorized constructor throws
it where it reads one, before it has touched the `HttpClient` you passed in.

What counts as readable is a full calendar date, then `T` or a space, then the time, with or without an offset
— what a server writes with the round-trip (`"O"`) format. A bare time of day and a date in a local convention
are refused rather than read as some point in time that means nothing.

A client constructed from an `AuthResponse` holds no credentials. It works for as long as its token is valid
and its refresh token is accepted; once the token has expired with no refresh token left to redeem, or with one
the server refused, there is nothing to re-authenticate with and the next request throws
`SessionNotRenewableException`. That is the point at which a new client has to be constructed from credentials
— it is not a failure the client can retry past. The exception derives from `InvalidOperationException`, so
`catch (InvalidOperationException)` catches it, but catching the narrower type keeps unrelated failures out of
your "sign in again" path. When the server refused the refresh token, its answer is
carried as an `HttpRequestException` in `InnerException`, with `StatusCode` set; when the client had no refresh
token, `InnerException` is null. A `404` there points at a misconfigured `refreshEndpoint` rather than a spent
session.

A refresh the server could not answer — `408`, `429` or a `5xx` — does not spend the session. The client keeps
the refresh token and throws `HttpRequestException` with `StatusCode` set to the answer, and the next call
tries the same refresh token again. A refresh that never reached the server keeps it too, and surfaces as the
`HttpRequestException` from `HttpClient`, with `StatusCode` null.

```csharp
try
{
    await client.GetAsync("api/data");
}
catch (SessionNotRenewableException)
{
    // The saved session is spent: delete auth-state.json and sign in again from credentials.
}
catch (HttpRequestException exception) when (exception.StatusCode is not null)
{
    // The server answered but could not renew now. The session is intact; retry later.
}
```

### Token Expiration
The client automatically handles token expiration:
1. Detects when token expires within 5 minutes
2. Automatically re-authenticates before making requests
3. Retries failed requests once with a fresh token

### Logging Out

`LogoutAsync()` posts the current refresh token to the server's logout endpoint (default `/api/auth/logout`) and then clears all local auth state. The server call is best-effort — HTTP failures do not prevent local state from being cleared, so the client always ends up in an unauthenticated state after the call.

```csharp
using HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/");
using AuthorizedHttpClient client = new AuthorizedHttpClient(
    httpClient, username: "user", password: "pass");

await client.GetAsync("api/data");

// Later — revoke the refresh token family and clear local state.
await client.LogoutAsync();

// Subsequent requests will trigger a fresh authentication flow
// (only possible when the client has credentials — API key or username/password.
// A pre-authorized client cannot re-authenticate after logout.)
```

You can override the logout endpoint path via the `logoutEndpoint` constructor parameter if your server mounts it elsewhere.

## Best Practices

### 1. HttpClient Lifecycle Management

The `AuthorizedHttpClient` doesn't dispose the underlying `HttpClient`. Manage the `HttpClient` lifecycle according to your application's needs:

```csharp
// For long-lived applications
HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/");

// Reuse the same AuthorizedHttpClient instance
AuthorizedHttpClient authorizedClient = new AuthorizedHttpClient(httpClient, "api-key");

// Use throughout your application
// Don't dispose the AuthorizedHttpClient unless you're done with the HttpClient
```

### 2. Error Handling

Always handle authentication and network errors:

```csharp
using (HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/"))
{
    AuthorizedHttpClient authorizedClient = new AuthorizedHttpClient(httpClient, "api-key");
    
    try
    {
        HttpResponseMessage response = await authorizedClient.GetAsync("api/data");

        // Checked here rather than with EnsureSuccessStatusCode(), which would throw an HttpRequestException
        // with StatusCode set and land your own API's failure in the sign-in branch below.
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"api/data failed: {(int)response.StatusCode}");
            return;
        }

        string content = await response.Content.ReadAsStringAsync();
        // Process response
    }
    catch (UnauthorizedAccessException)
    {
        // Handle credentials the server rejected (the auth endpoint answered 401)
    }
    catch (InvalidAuthResponseException exception)
    {
        // The address answered, successfully, with something that is not an auth response — so it is
        // most likely not an auth server. Say the host-specific thing here: the library knows the
        // address answered wrongly, and you know which addresses are supposed to be yours.
        Console.WriteLine($"{exception.RequestUri} is not the API. Check the configured server address.");
    }
    catch (HttpRequestException exception) when (exception.StatusCode is not null)
    {
        // The auth server answered with an unsuccessful status. Only a 401 from the auth endpoint
        // surfaces as UnauthorizedAccessException; any other status lands here, with StatusCode set.
        Console.WriteLine($"Sign-in failed: {(int)exception.StatusCode}");
    }
    catch (HttpRequestException)
    {
        // A transport failure, such as a refused connection or a DNS failure: StatusCode is null.
    }
}
```

Every unsuccessful status this client throws as an `HttpRequestException` carries `StatusCode`, and a
transport failure from `HttpClient` leaves it `null`. So around the client's own calls,
`exception.StatusCode is not null` separates "the server answered with a failure" from "the request did not
complete". `HttpRequestError` cannot make that split, since both can read `Unknown`.

Note that construction itself throws when a credential carries nothing — `ArgumentNullException` for `null`, `ArgumentException` for an empty string. If your credentials come from configuration that may be unset, validate them before constructing the client, or the throw lands on the constructor line rather than inside the `try` above.

## Migration

### From 1.8.0

One case changes type: a client constructed from an `AuthResponse` whose refresh is answered `408`, `429` or
`5xx` used to throw `InvalidOperationException` and now throws `HttpRequestException`. Every other existing
`catch` block keeps catching what it caught before.

**A spent session throws `SessionNotRenewableException`.** A client constructed from an `AuthResponse` whose
token has expired, with no refresh token left or one the server refused, previously threw a plain
`InvalidOperationException`. After a refusal, the server's answer is now its `InnerException`, as an
`HttpRequestException` with `StatusCode` set. The new type derives from `InvalidOperationException`, so
`catch (InvalidOperationException)` still works;
switch to `catch (SessionNotRenewableException)` to stop catching unrelated failures in that path. An
`Assert.ThrowsExceptionAsync<InvalidOperationException>` in your tests matches the exact type and needs the new
one. Its constructors are public, so a fake in your own tests can throw one.

**A refresh answered `408`, `429` or `5xx` keeps the refresh token.** It was previously cleared on any
unsuccessful status, so a server hiccup during refresh spent the session of a client without credentials. Now
only a refusal clears it. A client with credentials still falls back to signing in; a client without throws
`HttpRequestException` with `StatusCode` set, and the next call tries the same refresh token again.

**A refused sign-in sets `HttpRequestException.StatusCode`.** A non-401 answer from the auth endpoint was
thrown with `StatusCode` null. It now carries the response's status; message and type are unchanged. If you
were parsing the status out of the message, read `StatusCode` instead. A `401` still throws
`UnauthorizedAccessException`.

### From 1.7.0

**`AuthResponse.FromJson` now rejects JSON that is not an auth response.** It previously returned an instance
whose non-nullable properties were null when the JSON carried no `token` or no `expiresAt`, and returned one
carrying an unreadable `expiresAt` unchecked — both failed later, at the first use, with an exception naming
nothing that could be acted on. All of those now throw `ArgumentException`, as does an `expiresAt` that is not
an ISO 8601 date and time and a field written out as `null`. If you were relying on the old behaviour to hold
a half-populated response, read the fields from your own model instead.

**An expiry is read as UTC.** `AuthResponse.ExpirationTime` returns the instant the wire value names, converted
from whatever offset it carried; the client's own expiry checks use it. A server sending a non-UTC round-trip
value was previously compared against a UTC clock as though it were UTC, so its tokens read as valid for the
length of the offset after they expired. If you were compensating for that skew, stop.

**An auth or refresh endpoint that answers successfully with something that is not an auth response now throws
`InvalidAuthResponseException`.** It previously surfaced as a bare `ArgumentException` about deserialization,
naming neither the address that answered nor what it said. The new exception carries both; see [When the
Address Is Not an Auth Server](#when-the-address-is-not-an-auth-server). A `catch (ArgumentException)` around
an authenticating call no longer catches it.

**Bodies from an auth endpoint are read under a 64 K character cap, and redacted before they reach a message.** This
covers the unsuccessful branch too, so the body quoted in an `UnauthorizedAccessException` or
`HttpRequestException` message is now capped and has secret-looking values replaced with `[REDACTED]`. If you
were parsing the full error body out of the message, read it from the server's response instead.

**`AuthResponse.ToString()` writes the refresh token as `null` when there is none**, rather than leaving the
field out. Both forms are redacted; only the shape of the log line changed.

### From 1.6.0

**Both credential constructors now reject an empty credential.** `new AuthorizedHttpClient(httpClient, "")` and `new AuthorizedHttpClient(httpClient, "", "")` previously constructed successfully and failed later, at the first request; they now throw `ArgumentException` at construction. `null` continues to throw `ArgumentNullException`, and a whitespace-only credential is still sent to the server unchanged.

If you were relying on the old behaviour to defer credential validation to the server, move that check ahead of the constructor.

**The username/password constructor now normalizes the base address.** It previously skipped the trailing-slash normalization the other two constructors perform, so a base address carrying a path prefix (`https://api.example.com/myapp`) lost that prefix when the auth endpoint was appended — the login POST went to `https://api.example.com/api/auth/login`. If you worked around this by passing an absolute `authEndpoint`, or by adding the trailing slash yourself, that workaround is no longer needed (and remains harmless).

**The request models now pin their wire field names.** `LoginAuthRequest` and `ApiKeyAuthRequest` carry `[JsonPropertyName]` attributes and expose the names as constants (`UsernameFieldName`, `PasswordFieldName`, `ApiKeyFieldName`, `ClientIdFieldName`), matching the server-side models. The body this client sends is unchanged; it can no longer drift if a property is renamed or if `JsonSerializerSettings.CurrentOptions` is assigned a different naming policy.