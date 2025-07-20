# Creating ResourceManager Instances

This guide explains how to create and configure ResourceManager instances for your application.

## Basic Usage

### Simple Creation (Auto-Discovery)

For basic scenarios with embedded resources only:

```csharp
ResourceManager resourceManager = await ResourceManager.CreateInstanceAsync();
```

The ResourceManager will automatically discover the calling assembly and find all resource collections within it.

### With Assembly Specification

When you need to specify a particular assembly (common in testing scenarios):

```csharp
ResourceManager resourceManager = await ResourceManager.CreateInstanceAsync(
    Assembly.GetAssembly(typeof(Program)));
```

### With Predefined Providers

For providers that require configuration (API keys, connection strings, etc.):

```csharp
// Create provider instances
HttpApiResourceProvider apiProvider = new HttpApiResourceProvider("https://api.example.com", "api-key");
FileSystemResourceProvider fileProvider = new FileSystemResourceProvider(@"C:\Resources");

// Create predefined providers
PredefinedResourceProvider apiPredefinedProvider = new PredefinedResourceProvider(
    typeof(RemoteResources), apiProvider);
PredefinedResourceProvider filePredefinedProvider = new PredefinedResourceProvider(
    typeof(LocalResources), fileProvider);

// Create ResourceManager with providers
ResourceManager resourceManager = await ResourceManager.CreateInstanceAsync(
    apiPredefinedProvider, 
    filePredefinedProvider);
```

## ASP.NET Core Integration

In ASP.NET Core applications, register the ResourceManager as a singleton:

```csharp
// In Program.cs or Startup.cs
PredefinedResourceProvider modelsProvider = ByteShelfResourceProvider.CreatePredefined(
    resourceCollectionType: typeof(Resources.Models),
    baseUrl: EnvironmentVariables.GetVariable(EnvironmentVariable.ByteShelfUrl),
    apiKey: EnvironmentVariables.GetVariable(EnvironmentVariable.ByteShelfApiKey));

ResourceManager resourceManager = await ResourceManager.CreateInstanceAsync(modelsProvider);
builder.Services.AddSingleton(resourceManager);
```

Then inject it into your services:

```csharp
public class MyService
{
    private readonly ResourceManager resourceManager;

    public MyService(ResourceManager resourceManager)
    {
        this.resourceManager = resourceManager;
    }

    public async Task<string> GetConfigAsync()
    {
        return await resourceManager.ReadAsStringAsync(Resources.ConfigFile);
    }
}
```

## Key Points

- **Assembly Discovery**: If no assembly is specified, ResourceManager automatically finds the calling assembly. Explicitly specifying the assembly even if it's just by sending `Assembly.GetExecutingAssembly()` is good.
- **Provider Registration**: Use `PredefinedResourceProvider` to associate provider instances with resource collection types
- **Singleton Pattern**: Create one ResourceManager instance and reuse it throughout your application
- **Dependency Injection**: Register as a singleton in ASP.NET Core for easy injection into services
- **Startup Validation**: ResourceManager validates all resources exist during creation, preventing runtime errors

## Error Handling

ResourceManager creation will throw `InvalidOperationException` if:
- Resource collections are missing their providers
- Resources cannot be found by their providers
- Duplicate resource paths exist across collections
- Provider constructors require parameters that cannot be automatically injected

The error messages are designed to be clear and actionable, helping you quickly identify and fix configuration issues. 