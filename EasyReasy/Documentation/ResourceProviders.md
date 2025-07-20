# Resource Providers

This guide explains how to implement custom resource providers for EasyReasy. Resource providers are the core abstraction that allows EasyReasy to access resources from different sources.

## IResourceProvider Interface

The `IResourceProvider` interface defines the contract that all resource providers must implement:

```csharp
public interface IResourceProvider
{
    /// <summary>
    /// Checks if the specified resource exists.
    /// </summary>
    /// <param name="resource">The resource to check.</param>
    /// <returns>True if the resource exists; otherwise, false.</returns>
    Task<bool> ResourceExistsAsync(Resource resource);

    /// <summary>
    /// Gets a stream for reading the specified resource.
    /// </summary>
    /// <param name="resource">The resource to read.</param>
    /// <returns>A stream for reading the resource.</returns>
    Task<Stream> GetResourceStreamAsync(Resource resource);
}
```

The interface also provides default implementations for common operations that can be used as-is or overridden if needed:

- `ReadAsBytesAsync(Resource resource)` - Reads the resource as a byte array
- `ReadAsStringAsync(Resource resource)` - Reads the resource as a string

> These methods have working default implementations that use GetResourceStreamAsync internally, so they don't need to be manually implemented unless you want to provide a more optimized version for your specific provider.

## Implementing a Custom Provider

### Basic Example: File System Provider

Here's a simple example of a provider that reads files from the file system:

```csharp
public class FileSystemResourceProvider : IResourceProvider
{
    private readonly string baseDirectory;

    public FileSystemResourceProvider(string baseDirectory)
    {
        this.baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
    }

    public async Task<bool> ResourceExistsAsync(Resource resource)
    {
        await Task.CompletedTask; // Make async for consistency
        string fullPath = Path.Combine(baseDirectory, resource.Path);
        return File.Exists(fullPath);
    }

    public async Task<Stream> GetResourceStreamAsync(Resource resource)
    {
        await Task.CompletedTask; // Make async for consistency
        string fullPath = Path.Combine(baseDirectory, resource.Path);
        
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Resource '{resource.Path}' not found at '{fullPath}'.");

        return File.OpenRead(fullPath);
    }
}
```
> As you can see in the example above (and below) variables that the resource provider needs to function should be sent as parameters to the constructor.

### HTTP API Provider

Here's an example that fetches resources from a REST API:

```csharp
public class HttpApiResourceProvider : IResourceProvider
{
    private readonly HttpClient httpClient;
    private readonly string baseUrl;

    public HttpApiResourceProvider(string baseUrl, string apiKey = null)
    {
        this.baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        this.httpClient = new HttpClient();
        
        if (!string.IsNullOrEmpty(apiKey))
        {
            httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public async Task<bool> ResourceExistsAsync(Resource resource)
    {
        try
        {
            HttpResponseMessage response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, 
                $"{baseUrl}/resources/{Uri.EscapeDataString(resource.Path)}"));
            
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<Stream> GetResourceStreamAsync(Resource resource)
    {
        HttpResponseMessage response = await httpClient.GetAsync($"{baseUrl}/resources/{Uri.EscapeDataString(resource.Path)}");
        
        if (!response.IsSuccessStatusCode)
            throw new FileNotFoundException($"Resource '{resource.Path}' not found on server.");

        return await response.Content.ReadAsStreamAsync();
    }

    public void Dispose()
    {
        httpClient?.Dispose();
    }
}
```

## Using Your Custom Provider

### 1. Define Resource Collections

Create resource collections that use your custom provider:

```csharp
[ResourceCollection(typeof(FileSystemResourceProvider))]
public static class LocalResources
{
    public static readonly Resource ConfigFile = new Resource("config/appsettings.json");
    public static readonly Resource LogTemplate = new Resource("templates/log.html");
}
```

### 2. Register Providers with ResourceManager

When creating the ResourceManager, register your custom providers using `PredefinedResourceProvider`. This class associates a specific resource collection type with a provider instance, allowing the ResourceManager to know which provider to use for each collection:

```csharp
// Create provider instances
FileSystemResourceProvider fileProvider = new FileSystemResourceProvider(@"C:\AppData\Resources");
HttpApiResourceProvider apiProvider = new HttpApiResourceProvider("https://api.example.com", "your-api-key");

// Create predefined providers - these associate collection types with provider instances
PredefinedResourceProvider filePredefinedProvider = new PredefinedResourceProvider(
    typeof(LocalResources), fileProvider);
PredefinedResourceProvider apiPredefinedProvider = new PredefinedResourceProvider(
    typeof(ApiResources), apiProvider);

// Create ResourceManager with all providers
ResourceManager manager = await ResourceManager.CreateInstanceAsync(
    filePredefinedProvider, 
    apiPredefinedProvider);
```

### 3. Access Resources

Use the ResourceManager to access resources from your custom providers:

```csharp
// Access file system resources
string configContent = await manager.ReadAsStringAsync(LocalResources.ConfigFile);

// Access API resources
byte[] apiData = await manager.ReadAsBytesAsync(ApiResources.UserProfile);
```

## Advanced Features

### Implementing ICacheableResourceProvider

If your provider can benefit from caching, implement `ICacheableResourceProvider`. This is particularly useful for providers that access remote resources or have expensive operations.

> For detailed information about implementing caching in your resource providers, see [Caching.md](Caching.md).

The `ICacheableResourceProvider` interface adds three methods to the standard `IResourceProvider`:

- `GetCache()` - Returns the cache instance used by this provider
- `IsCacheStaleAsync(Resource)` - Checks if the cached version needs updating
- `UpdateCacheAsync(Resource)` - Updates the cache with the latest version

Caching can significantly improve performance for remote resources and provide offline capabilities.

### Error Handling Best Practices

Implement proper error handling in your providers:

```csharp
public async Task<Stream> GetResourceStreamAsync(Resource resource)
{
    try
    {
        // Your implementation here
        return await FetchResourceFromSource(resource);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        throw new FileNotFoundException($"Resource '{resource.Path}' not found.", ex);
    }
    catch (HttpRequestException ex)
    {
        throw new InvalidOperationException($"Failed to access resource '{resource.Path}'. Check network connectivity.", ex);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Unexpected error accessing resource '{resource.Path}'.", ex);
    }
}
```

## Testing Your Provider

Create unit tests for your custom provider:

```csharp
[TestClass]
public class CustomProviderTests
{
    [TestMethod]
    public async Task ResourceExistsAsync_WithExistingResource_ReturnsTrue()
    {
        // Arrange
        FileSystemResourceProvider provider = new FileSystemResourceProvider(@"C:\TestResources");
        
        // Act
        bool exists = await provider.ResourceExistsAsync(new Resource("test.txt"));
        
        // Assert
        Assert.IsTrue(exists);
    }

    [TestMethod]
    public async Task GetResourceStreamAsync_WithNonExistingResource_ThrowsFileNotFoundException()
    {
        // Arrange
        FileSystemResourceProvider provider = new FileSystemResourceProvider(@"C:\TestResources");
        
        // Act & Assert
        await Assert.ThrowsExceptionAsync<FileNotFoundException>(() =>
            provider.GetResourceStreamAsync(new Resource("nonexistent.txt")));
    }
}
```

## Summary

Custom resource providers allow you to extend EasyReasy to work with any data source. The key points are:

1. **Implement `IResourceProvider`** - Define how to check existence and read resources
2. **Use meaningful error messages** - Help users understand what went wrong
3. **Consider caching** - Implement `ICacheableResourceProvider` for performance
4. **Test thoroughly** - Ensure your provider works reliably
5. **Register with ResourceManager** - Use `PredefinedResourceProvider` to connect your provider to resource collections

With custom providers, EasyReasy becomes a unified interface for accessing resources from any source while maintaining the safety and validation benefits of the framework. 