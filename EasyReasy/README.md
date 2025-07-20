# EasyReasy

A lightweight .NET library for managing and accessing resources from various sources with startup-time validation and runtime safety.

## Overview

EasyReasy provides a unified way to access resources from different sources (embedded files, remote APIs, environment variables, etc.) while ensuring they exist at startup, preventing runtime errors during execution.

## Core Concepts

### Resource
A `Resource` represents a file with a path. It's a simple struct that provides utility methods for content type detection and file operations. **Important**: Resources should only be created within ResourceCollections, not directly using the constructor.

```csharp
// ❌ Don't do this - creates a resource without validation
Resource configFile = new Resource("config/appsettings.json");

// ✅ Instead, define Resources in ResourceCollections
```

### ResourceCollection
A static class marked with `[ResourceCollection]` that defines resources and their provider:

```csharp
[ResourceCollection(typeof(EmbeddedResourceProvider))] // Specify the type of the IResourceProvider for this collection
public static class AppResources
{
    public static readonly Resource ConfigFile = new Resource("config/appsettings.json");
    public static readonly Resource TemplateFile = new Resource("templates/default.html");
}
```

> Only define a resource in one place. Don't include duplicated resources as it will cause errors.

### ResourceProvider
An `IResourceProvider` defines how to access resources from a specific source. The library includes `EmbeddedResourceProvider` which is for accessing resources that are embedded resources in assemblies.

If you want to access resources from somewhere that is not already supported, you can just implement `IResourceProvider` and then use your own implementation of it.

> For detailed information about implementing custom resource providers, see [Documentation/ResourceProviders.md](Documentation/ResourceProviders.md).

### ResourceManager
The central component that discovers resource collections, validates their existence, and provides access to resources:

```csharp
// Create and validate all resources at startup
ResourceManager resourceManager = await ResourceManager.CreateInstanceAsync();

// Access resources safely
string configContent = await resourceManager.ReadAsStringAsync(AppResources.ConfigFile);
```

> For detailed information about creating ResourceManager instances, see [Documentation/ResourceManager.md](Documentation/ResourceManager.md).

## Getting Started

### 1. Define Your Resources

Create resource collections in your project:

```csharp
public static class Resources
{
    [ResourceCollection(typeof(EmbeddedResourceProvider))]
    public static class Models
    {
        public static readonly Resource TesseractModel = new Resource("models/eng.traineddata");
        public static readonly Resource ConfigModel = new Resource("models/config.json");
    }

    [ResourceCollection(typeof(EmbeddedResourceProvider))]
    public static class Templates
    {
        public static readonly Resource UserTemplate = new Resource("templates/user.html");
        public static readonly Resource ApiConfig = new Resource("config/api.json");
    }
}
```
 
> The outer class `Resources` isn't strictly required but it's a nice way to group all resource collections in a single place allowing nice syntax like `Resources.Models.TesseractModel` as well as making it clear to other developers or your future self where the resource defintions are.

### 2. Initialize ResourceManager

Create the ResourceManager early in your application startup:

```csharp
// For embedded resources only
ResourceManager resourceManager = await ResourceManager.CreateInstanceAsync();
```

This will automatically discover all resource collections and verify that the providers are set up correctly and that the resources can actually be found. Any errors will be reported in an easily readable message in the exception that is thrown if an error is encountered.

> For providers that require configuration (like API keys or connection strings), use `PredefinedResourceProvider`:

Here is an example of using `PredefinedResourceProvider`:

```csharp
// Create provider instances with configuration
HttpApiResourceProvider apiProvider = new HttpApiResourceProvider(baseUrl: "https://api.example.com", apiKey: "your-api-key");
FileSystemResourceProvider fileProvider = new FileSystemResourceProvider(basePath: @"C:\AppData\Resources");

// Create predefined providers
PredefinedResourceProvider apiPredefinedProvider = new PredefinedResourceProvider(
    typeof(RemoteResources), apiProvider);

PredefinedResourceProvider filePredefinedProvider = new PredefinedResourceProvider(
    typeof(LocalResources), fileProvider);

// Create ResourceManager with all providers
ResourceManager resourceManager = await ResourceManager.CreateInstanceAsync(
    apiPredefinedProvider, 
    filePredefinedProvider);
```

> When creating a `PredefinedResourceProvider` a type for a specific resource collection is provided, this is so that different resource collections can have the same resource provider type but still have different secrets.

### 3. Access Resources

Use the ResourceManager to safely access your resources:

```csharp
// Read as string
string template = await resourceManager.ReadAsStringAsync(Resources.Templates.UserTemplate);

// Read as bytes
byte[] modelData = await resourceManager.ReadAsBytesAsync(Resources.Models.TesseractModel);

// Get stream
using Stream stream = await resourceManager.GetResourceStreamAsync(Resources.Models.ConfigModel);
```
> Accessing them in the simple way without having to worry about if they exist for real or not like in the code above is made possible by the resourceManager instance being created at startup and then verifying the existance of the resources so that no run time exception will occur in the middle of execution.

### Testing

It's recommended to add a test that verifies all resources exist:

```csharp
[TestMethod]
public async Task AllResourcesExist()
{
    // This will throw if any resources are missing
    ResourceManager resourceManager = await ResourceManager.CreateInstanceAsync(Assembly.GetAssembly(typeof(Resources)));
    Assert.IsNotNull(resourceManager);
}
```

> **Important**: Use `Assembly.GetAssembly(typeof(Resources))` (or any type from your main assembly like `Program`) to ensure the ResourceManager validates resources in your main application assembly, not the test assembly. This prevents the test from passing even when resources are missing from your actual application.

## Advanced Features

### Caching Support

Providers can implement `ICacheableResourceProvider` to support caching:

```csharp
FileSystemCache cache = new FileSystemCache("/path/to/cache");
// Custom providers can use caching
```


## Why ResourceManager Matters

- **Startup-time validation**: Resources are validated at startup, not during execution
- **Early error detection**: Missing resources cause immediate failures, not runtime surprises
- **Type safety**: Resources are strongly typed and IntelliSense-friendly
- **Provider abstraction**: Switch data sources without changing resource access code
- **Caching support**: Optional caching for performance optimization

## Best Practices

1. **Create ResourceManager early**: Initialize during application startup to catch missing resources immediately
2. **Use meaningful resource paths**: Organize resources logically (e.g., `models/`, `templates/`, `config/`)
3. **Add validation tests**: Include unit tests that create ResourceManager to verify all resources exist
4. **Group related resources**: Use separate resource collections for different types of resources
5. **Document resource purposes**: Add XML comments to explain what each resource is for

## Provider Examples

### Embedded Resources
```csharp
[ResourceCollection(typeof(EmbeddedResourceProvider))]
public static class EmbeddedResources
{
    public static readonly Resource DefaultConfig = new Resource("config/default.json");
}
```

### Custom Provider
```csharp
public class DatabaseResourceProvider : IResourceProvider
{
    // Implementation for database-stored resources
}

[ResourceCollection(typeof(DatabaseResourceProvider))]
public static class DatabaseResources
{
    public static readonly Resource UserProfile = new Resource("profiles/default.json");
}
```

EasyReasy makes resource management simple, safe, and predictable. Start using it today to eliminate runtime resource errors! 