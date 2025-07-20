# EasyReasy.ByteShelfProvider

A resource provider for EasyReasy that integrates with ByteShelf, enabling access to files stored in ByteShelf's hierarchical subtenant structure.

## Overview

ByteShelfResourceProvider maps EasyReasy resource paths to ByteShelf's subtenant hierarchy, allowing you to access files stored in ByteShelf through the familiar EasyReasy resource system. It supports optional local caching for improved performance.

## Key Features

- **ByteShelf Integration**: Access files stored in ByteShelf's subtenant hierarchy
- **Path Mapping**: Resource paths map to subtenant display names and file names
- **Caching Support**: Optional local caching with automatic cache invalidation
- **Hierarchical Navigation**: Traverse subtenant hierarchy using display names
- **Latest File Selection**: Automatically selects the newest version when multiple files with the same name exist

## Installation

```bash
dotnet add package EasyReasy.ByteShelfProvider
```

## Basic Usage

### Creating a Resource Provider

```csharp
using EasyReasy.ByteShelfProvider;

// Basic setup
ByteShelfResourceProvider provider = new ByteShelfResourceProvider(
    baseUrl: "https://api.byteshelf.com",
    apiKey: "your-api-key");

// With optional root subtenant and caching
ByteShelfResourceProvider provider = new ByteShelfResourceProvider(
    baseUrl: "https://api.byteshelf.com",
    apiKey: "your-api-key",
    rootSubTenantId: "root-tenant-id",
    cache: new FileSystemCache("cache-directory"));
```

> Explicitly specifying rootSubTenantId is rarely used but specifying a cache is **highly recommended**.

### Using with Resource Collections

```csharp
[ResourceCollection(typeof(ByteShelfResourceProvider))]
public static class MyResources
{
    public static readonly Resource VisionModel = new Resource("models/ML-vision.onnx");
    public static readonly Resource TemplateFile = new Resource("templates/email.html");
}
```

```csharp
// Create a predefined provider for your resource collection
PredefinedResourceProvider byteShelfProvider = ByteShelfResourceProvider.CreatePredefined(
    resourceCollectionType: typeof(MyResources),
    baseUrl: "https://api.byteshelf.com",
    apiKey: "your-api-key");

// Use with ResourceManager
ResourceManager resourceManager = await ResourceManager.CreateInstanceAsync(
    assembly: Assembly.GetExecutingAssembly(),
    predefinedProviders: byteShelfProvider);

// Access resources
Stream modelStream = await resourceManager.GetResourceStreamAsync(MyResources.VisionModel);
```

## Path Mapping

Resource paths are mapped to ByteShelf's subtenant hierarchy:

- **Path**: `"config/settings.json"`
- **Mapping**: 
  - `"config"` → Subtenant with display name "config"
  - `"settings.json"` → File with display name "settings.json"

> If using the `ByteShelf`-ui the "display name" is simply the name that is displayed to you. (ByteShelf stores files as GUIDs behind the scenes)

The provider traverses the subtenant hierarchy using display names and throws `DirectoryNotFoundException` if any segment is missing.

## Caching

ByteShelfResourceProvider implements `ICacheableResourceProvider` for optional caching:

```csharp
// With file system cache
IResourceCache fileSystemCache = new FileSystemCache("cache-directory");
ByteShelfResourceProvider provider = new ByteShelfResourceProvider(
    baseUrl: "https://api.byteshelf.com",
    apiKey: "your-api-key",
    cache: fileSystemCache);
```

### Cache Behavior

- **Automatic Caching**: Downloaded files are cached locally
- **Cache Invalidation**: Cache is invalidated when ByteShelf file timestamps are newer
- **Stale Detection**: `IsCacheStaleAsync()` checks if cached version needs updating
- **Cache Updates**: `UpdateCacheAsync()` downloads and stores the latest version

## Error Handling

The provider throws appropriate exceptions for common scenarios:

- `FileNotFoundException`: When the requested file doesn't exist in ByteShelf
- `DirectoryNotFoundException`: When a subtenant in the path doesn't exist
- `ArgumentException`: When resource path is empty or invalid
- `InvalidOperationException`: When ByteShelf API calls fail

## Dependencies

- **EasyReasy**: Core resource management functionality
- **ByteShelfClient**: ByteShelf API client
- **ByteShelfCommon**: Shared ByteShelf types and utilities (used by `ByteShelfClient`)

## Best Practices

1. **Use Resource Collections**: Define your resources in static classes with `ResourceCollectionAttribute`
2. **Implement Caching**: Use caching for frequently accessed files to improve performance and ensure offline access is supported
3. **Handle Errors**: Implement proper error handling for network and API failures
4. **Validate Paths**: Ensure resource paths match your ByteShelf subtenant structure
5. **Monitor Cache**: Use cache invalidation methods to ensure data freshness

## Example: ASP.NET Core Integration

```csharp
// Program.cs
builder.Services.AddSingleton<ResourceManager>(serviceProvider =>
{
    ResourceManager resourceManager = new ResourceManager();
    
    PredefinedResourceProvider provider = ByteShelfResourceProvider.CreatePredefined(
        resourceCollectionType: typeof(MyResources),
        baseUrl: EnvironmentVariables.GetVariable(EnvironmentVariable.ByteShelfBaseUrl),
        apiKey: EnvironmentVariables.GetVariable(EnvironmentVariable.ByteShelfApiKey),
        cache: new FileSystemCache(storagePath: "ByteShelfCache"));
    
    resourceManager.RegisterProvider(provider);
    return resourceManager;
});
```

> In a real scenarion the storage path for the cache would probably come from an environment variable and if hosting with a systemd service in linux that would probably be set to /var/lib/your-service