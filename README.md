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

The foundation library that provides:
- Resource abstraction and management
- Resource collections with provider specification
- ResourceManager for startup-time validation
- Embedded resource provider
- Custom provider framework
- Caching support

### [EasyReasy.EnvironmentVariables](EasyReasy.EnvironmentVariables/README.md)
[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.EnvironmentVariables-blue.svg)](https://www.nuget.org/packages/EasyReasy.EnvironmentVariables)

Environment variable management with:
- Startup-time validation of environment variables
- Type-safe environment variable access
- Minimum length validation
- Safe retrieval with error handling
- File-based environment variable loading

### [EasyReasy.ByteShelfProvider](EasyReasy.ByteShelfProvider/README.md)
[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.ByteShelfProvider-blue.svg)](https://www.nuget.org/packages/EasyReasy.ByteShelfProvider)

ByteShelf integration providing:
- Remote file access via ByteShelf API
- Hierarchical subtenant navigation
- Optional local caching
- Path mapping to ByteShelf structure
- Latest file version selection

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
