# EasyReasy System

EasyReasy began as a simple library for easily handling embedded resources in .NET applications. What started as a focused solution for loading embedded files has grown into a comprehensive ecosystem that maintains the same philosophy: **easy to use, type-safe, and self-documenting APIs**.

## The Evolution

### The Beginning: EasyReasy
It all started with **EasyReasy** - a library designed to make loading embedded resources effortless. With startup-time validation and a clean, intuitive API, it provided a simple way to access embedded files with full type safety.

### Expanding to External Resources
As the system grew, **EasyReasy.ByteShelfProvider** was added to extend the same easy loading experience to remote and external resources. The same familiar patterns and type safety were maintained, just applied to different data sources.

### Environment Variable Management
**EasyReasy.EnvironmentVariables** followed, bringing the same easy-to-use philosophy to environment variable handling. No more unsafe string access - just statically typed, validated environment variable retrieval with compile-time safety.

### Authentication and Security
**EasyReasy.Auth** and **EasyReasy.Auth.Client** were introduced to provide seamless authentication experiences. JWT token management, claims handling, and client-side authentication all follow the same principles of simplicity and type safety.

### Vector Storage and AI
**EasyReasy.VectorStorage** represents the latest expansion, offering high-performance vector similarity search with cosine similarity. Even in the complex world of AI and vector operations, the same easy-to-use approach prevails.

## Projects

| Project | NuGet | Description |
|---------|-------|-------------|
| [EasyReasy](EasyReasy/README.md) | [![NuGet](https://img.shields.io/badge/nuget-EasyReasy-blue.svg)](https://www.nuget.org/packages/EasyReasy) | Core resource management with startup-time validation |
| [EasyReasy.EnvironmentVariables](EasyReasy.EnvironmentVariables/README.md) | [![NuGet](https://img.shields.io/badge/nuget-EasyReasy.EnvironmentVariables-blue.svg)](https://www.nuget.org/packages/EasyReasy.EnvironmentVariables) | Environment variable validation and safe retrieval |
| [EasyReasy.ByteShelfProvider](EasyReasy.ByteShelfProvider/README.md) | [![NuGet](https://img.shields.io/badge/nuget-EasyReasy.ByteShelfProvider-blue.svg)](https://www.nuget.org/packages/EasyReasy.ByteShelfProvider) | ByteShelf integration for remote file access |
| [EasyReasy.Auth](EasyReasy.Auth/README.md) | [![NuGet](https://img.shields.io/badge/nuget-EasyReasy.Auth-blue.svg)](https://www.nuget.org/packages/EasyReasy.Auth) | JWT authentication and claims handling |
| [EasyReasy.Auth.Client](EasyReasy.Auth.Client/README.md) | [![NuGet](https://img.shields.io/badge/nuget-EasyReasy.Auth.Client-blue.svg)](https://www.nuget.org/packages/EasyReasy.Auth.Client) | Lightweight client library for EasyReasy.Auth servers |
| [EasyReasy.VectorStorage](EasyReasy.VectorStorage/README.md) | [![NuGet](https://img.shields.io/badge/nuget-EasyReasy.VectorStorage-blue.svg)](https://www.nuget.org/packages/EasyReasy.VectorStorage) | High-performance vector similarity search with cosine similarity |

## Documentation

For detailed information about each component, see the individual project README files linked above. Each project includes comprehensive documentation with examples, best practices, and advanced usage patterns. 
