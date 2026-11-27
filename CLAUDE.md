# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

EasyReasy is a collection of independent .NET 10 NuGet packages sharing a common philosophy: type-safe, self-documenting APIs with startup-time validation. Each package is a class library published to NuGet. There is no runnable application here — only libraries and their tests.

## Build & Test Commands

```bash
dotnet build                                    # Build entire solution
dotnet test                                     # Run all tests
dotnet test EasyReasy.Auth.Tests                # Run tests for a specific project
dotnet test --filter "FullyQualifiedName~ClassName.MethodName" EasyReasy.Auth.Tests  # Run a single test
```

CI's main test job skips every `*IntegrationTests` class. The exception is the `*KeycloakIntegrationTests`, which run against a Keycloak container in the `keycloak` job of `tests.yml`, and in `publish.yml` before a package with a real sign-in flow is packed (`publish.yml` lists them). Without `KEYCLOAK_URL` they are inconclusive, so a plain `dotnet test` passes without Keycloak. `.github/scripts/keycloak.sh` holds the image, port and admin, and how the tests are run:

```bash
export KEYCLOAK_PORT=18080   # optional, default 8080; export it so start, wait and test all use it
.github/scripts/keycloak.sh start && .github/scripts/keycloak.sh wait
dotnet build -c Release && .github/scripts/keycloak.sh test EasyReasy.Auth.OpenIdConnect.Tests
.github/scripts/keycloak.sh test EasyReasy.Auth.Saml.Tests
```

Pushing a tag `<package>-v<version>` publishes that package through `publish.yml`, and the version must match the csproj's. A prerelease's tag carries its `<VersionSuffix>` too: `auth-openidconnect-v1.0.0-preview.1`.

## Solution Structure

Each library project has a corresponding test project:

| Library | Purpose | Dependencies |
|---------|---------|-------------|
| `EasyReasy` | Core resource management (embedded/file resources) | — |
| `EasyReasy.ByteShelfProvider` | Remote resource provider via ByteShelf | `EasyReasy` |
| `EasyReasy.EnvironmentVariables` | Typed environment variable validation & retrieval | — |
| `EasyReasy.Auth` | JWT auth, claims middleware, password hashing, TOTP/base32/secret-cipher MFA primitives, WebAuthn/FIDO2 second factor and passkey sign-in, query-string tokens for WebSockets, path-scoped cookie tokens for files opened in a new tab, an OAuth 2.1 authorization server with app-owned consent and store, a hosted sweep of expired rows for app-owned stores, and the building blocks browser-redirect sign-in packages share (`FlowBindingCookie`, `JsonSealer`) (ASP.NET Core) | — |
| `EasyReasy.Auth.Client` | Lightweight HTTP client for Auth servers | — |
| `EasyReasy.Auth.Google` | Google Sign-In integration for EasyReasy.Auth | `EasyReasy.Auth` |
| `EasyReasy.Auth.OpenIdConnect` | Sign-in with any OpenID Connect provider through the server-side code flow with PKCE, for EasyReasy.Auth: providers resolved per request by the app, and the public code flow engine a provider package builds on | `EasyReasy.Auth` |
| `EasyReasy.Auth.Microsoft` | Microsoft (Entra ID) sign-in for EasyReasy.Auth, on `EasyReasy.Auth.OpenIdConnect`'s code flow with Entra's tenants as providers and the tenant, authentication context and `tid` plus `oid` rules on top | `EasyReasy.Auth.OpenIdConnect` |
| `EasyReasy.Auth.Saml` | SAML 2.0 service-provider sign-in for EasyReasy.Auth: SP-initiated over HTTP-Redirect with identity providers resolved per request from app-supplied metadata, an app-owned store of sealed pending requests and completions consumed atomically, and its own checks of each response over ITfoxtec.Identity.Saml2's parsing, signatures and decryption; its tests drive an in-process fake identity provider through hostile responses, and its real flows run against Keycloak in a CI job of their own | `EasyReasy.Auth` |
| `EasyReasy.Auth.Testing` | Test helpers for consumers' test projects: a software WebAuthn authenticator verified against the real `WebAuthnVerifier`, and an OAuth test client and in-memory store that drive the OAuth authorization server; built on `EasyReasy.Auth`'s public API only, with no `InternalsVisibleTo` | `EasyReasy.Auth` |
| `EasyReasy.VectorStorage` | In-memory cosine similarity vector search | — |

`EasyReasy.ByteShelfProvider` depends on `EasyReasy`. `EasyReasy.Auth.Google`, `EasyReasy.Auth.OpenIdConnect`, `EasyReasy.Auth.Saml` and `EasyReasy.Auth.Testing` depend on `EasyReasy.Auth`. `EasyReasy.Auth.Microsoft` depends on `EasyReasy.Auth.OpenIdConnect`. All other libraries are fully independent of each other. `EasyReasy.Auth.Tests` uses `EasyReasy.Auth.Testing`'s authenticator for its key, COSE encoding and signatures, and its OAuth test client and in-memory store to drive the OAuth server; `EasyReasy.Auth.Testing.Tests` proves the authenticator's ceremonies against the real verifier, and the OAuth client and store against a real OAuth server. `EasyReasy.Auth.Tests` also holds `FakeBrowser`, the cookie jar the OpenID Connect, Microsoft and SAML test projects link in, and under `Keycloak/` the `KeycloakAdmin` and `KeycloakLoginBrowser` the OpenID Connect and SAML Keycloak tests link in, each adding only its protocol's client and response.

## Architecture

### Core Design Patterns

**Attribute-driven discovery + reflection**: `ResourceManager` discovers `[ResourceCollection]`-decorated classes at startup. `EnvironmentVariableHelper` discovers `[EnvironmentVariableNameContainer]` classes with `[EnvironmentVariableName]` fields. Both collect all validation errors into a single `InvalidOperationException` so developers fix everything at once.

**Readonly structs as typed identifiers**: `Resource`, `VariableName`, `VariableNameRange`, `StoredVector` are all `readonly struct` types providing value semantics over raw strings.

**Static async factory**: `ResourceManager.CreateInstanceAsync(...)` ensures validation completes before the instance is usable — constructors can't be async.

**Provider pattern with interface segregation**: `IResourceProvider` for basic access, `ICacheableResourceProvider` as a separate concern. `PredefinedResourceProvider` handles providers that need constructor arguments.

**Extension methods for fluent APIs**: Each library exposes extension methods for ergonomic use (e.g., `VariableName.GetValue()`, `IApplicationBuilder.UseEasyReasyAuth()`, `IResourceProvider.AsPredefinedFor(...)`).

**JSON serialization pattern**: Request/response models use `System.Text.Json` with a consistent pattern: constructor, `ToJson()` instance method, static `FromJson(string)` factory, `ToString()` delegates to `ToJson()`.

### Testing

All test projects use MSTest. Tests are organized into focused files by concern (e.g., `ResourceManagerBasicTests`, `ResourceManagerValidationTests`). Test helpers and fakes live alongside test files in subdirectories.

## Code Style

### C#
- Use explicit types — **never use `var`**
- Follow Microsoft C# conventions and identifier naming rules, prefer clear and readable names instead of short ones
- Nullable reference types enabled — don't bypass with `null!` or `string.Empty`
- Use `required` keyword or proper constructors to enforce nullability
- One public type per file, file name matches type name
- A library project is exactly one namespace, matching the project name. Subfolders organise files and do **not** nest the namespace — `EasyReasy.Auth.Something` names a *sibling NuGet package* (as `EasyReasy.Auth.Google` does), so a nested namespace would read as a package that does not exist. Types in a folder are therefore named to stand alone in the flat namespace.
- Do not use tuples for public return types
- Prefer block scoped namespaces and using statements
- Don't use `.Result()` for async calls, make everything async instead
- Test naming: `MethodName_Scenario_ExpectedBehavior`
- All public APIs have XML doc comments (`GenerateDocumentationFile` is enabled on all projects)

When implementing a feature or change, look at how similar features are done in other places first for reference. Strive to follow SOLID principles and consider proper dependency inversion and low coupling.

## Documentation

Each project has its own `README.md` with comprehensive usage examples, included in the NuGet package. The root [README.md](./README.md) serves as the solution index.
