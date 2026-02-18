# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

EasyReasy is a collection of independent .NET 8 NuGet packages sharing a common philosophy: type-safe, self-documenting APIs with startup-time validation. Each package is a class library published to NuGet. There is no runnable application here — only libraries and their tests.

## Build & Test Commands

```bash
dotnet build                                    # Build entire solution
dotnet test                                     # Run all tests
dotnet test EasyReasy.Auth.Tests                # Run tests for a specific project
dotnet test --filter "FullyQualifiedName~ClassName.MethodName" EasyReasy.Auth.Tests  # Run a single test
```

## Solution Structure

Six independent library projects, each with a corresponding test project:

| Library | Purpose | Dependencies |
|---------|---------|-------------|
| `EasyReasy` | Core resource management (embedded/file resources) | — |
| `EasyReasy.ByteShelfProvider` | Remote resource provider via ByteShelf | `EasyReasy` |
| `EasyReasy.EnvironmentVariables` | Typed environment variable validation & retrieval | — |
| `EasyReasy.Auth` | JWT auth, claims middleware, password hashing (ASP.NET Core) | — |
| `EasyReasy.Auth.Client` | Lightweight HTTP client for Auth servers | — |
| `EasyReasy.VectorStorage` | In-memory cosine similarity vector search | — |

Only `EasyReasy.ByteShelfProvider` depends on another library project (`EasyReasy`). All other libraries are fully independent of each other.

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
- Do not use tuples for public return types
- Prefer block scoped namespaces and using statements
- Don't use `.Result()` for async calls, make everything async instead
- Test naming: `MethodName_Scenario_ExpectedBehavior`
- All public APIs have XML doc comments (`GenerateDocumentationFile` is enabled on all projects)

When implementing a feature or change, look at how similar features are done in other places first for reference. Strive to follow SOLID principles and consider proper dependency inversion and low coupling.

## Documentation

Each project has its own `README.md` with comprehensive usage examples, included in the NuGet package. The root [README.md](./README.md) serves as the solution index.
