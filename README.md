# EasyReasy System

A comprehensive .NET resource management system that provides unified access to resources from various sources with startup-time validation and runtime safety.

## Overview

EasyReasy consists of three core projects that work together to provide a complete resource management solution:

- **EasyReasy**: Core resource management with embedded file support
- **EasyReasy.EnvironmentVariable**: Environment variable validation and safe retrieval
- **EasyReasy.ByteShelfProvider**: ByteShelf integration for remote file access

## Core Projects

### [EasyReasy](EasyReasy/README.md)
[![NuGet](https://img.shields.io/badge/nuget-EasyReasy-blue.svg)](https://www.nuget.org/packages/EasyReasy)

The foundation library providing resource abstraction, management, and startup-time validation with embedded file support and custom provider framework.

### [EasyReasy.EnvironmentVariables](EasyReasy.EnvironmentVariables/README.md)
[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.EnvironmentVariables-blue.svg)](https://www.nuget.org/packages/EasyReasy.EnvironmentVariables)

Environment variable management with startup-time validation, type-safe access, and minimum length validation for safe retrieval.

### [EasyReasy.ByteShelfProvider](EasyReasy.ByteShelfProvider/README.md)
[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.ByteShelfProvider-blue.svg)](https://www.nuget.org/packages/EasyReasy.ByteShelfProvider)

ByteShelf integration providing remote file access via API with hierarchical subtenant navigation and optional local caching.

### [EasyReasy.Auth](EasyReasy.Auth/README.md)
[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.Auth-blue.svg)](https://www.nuget.org/packages/EasyReasy.Auth)

JWT authentication and claims handling with automatic endpoints, claims injection middleware, and progressive delay protection.

## Key Benefits

- **Startup-time validation**: Catch missing resources early, not during execution
- **Type safety**: Strongly typed resource access with IntelliSense support
- **Provider abstraction**: Switch data sources without changing resource access code
- **Unified interface**: Access embedded files, environment variables, and remote resources through the same API
- **Caching support**: Optional caching for performance optimization
- **Error prevention**: Comprehensive validation prevents runtime surprises

## Quick Start

```csharp
// Define resources
[ResourceCollection(typeof(EmbeddedResourceProvider))]
public static class AppResources
{
    public static readonly Resource ConfigFile = new Resource("config/appsettings.json");
}

// Initialize with validation
ResourceManager resourceManager = await ResourceManager.CreateInstanceAsync();

// Access safely
string config = await resourceManager.ReadAsStringAsync(AppResources.ConfigFile);
```

## Documentation

For detailed information about each component, see the individual project README files linked above. Each project includes comprehensive documentation with examples, best practices, and advanced usage patterns. 
