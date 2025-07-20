# Resource Provider Caching

This guide explains how to implement caching in your custom resource providers using the `ICacheableResourceProvider` interface.

## ICacheableResourceProvider Interface

The `ICacheableResourceProvider` interface extends `IResourceProvider` to add caching capabilities:

```csharp
public interface ICacheableResourceProvider
{
    /// <summary>
    /// Gets the cache instance used by this resource provider.
    /// </summary>
    /// <returns>The cache instance, or null if no cache is configured.</returns>
    IResourceCache? GetCache();

    /// <summary>
    /// Checks if the cached version of a resource is stale and needs to be updated.
    /// </summary>
    /// <param name="resource">The resource to check.</param>
    /// <returns>True if the cache is stale and should be updated; otherwise, false.</returns>
    Task<bool> IsCacheStaleAsync(Resource resource);

    /// <summary>
    /// Updates the cache with the latest version of the resource.
    /// </summary>
    /// <param name="resource">The resource to update in cache.</param>
    /// <returns>A task that represents the asynchronous cache update operation.</returns>
    Task UpdateCacheAsync(Resource resource);
}
```

## Why Use Caching?

Caching is particularly useful for resource providers that:
- Access remote resources (HTTP APIs, cloud storage)
- Have expensive operations (database queries, file system access)
- Need to reduce network traffic or improve performance
- Want to provide offline capabilities

## Implementing a Cached Provider

Here's a complete example of a cached HTTP provider:

```csharp
public class CachedHttpProvider : IResourceProvider, ICacheableResourceProvider
{
    private readonly HttpClient httpClient;
    private readonly IResourceCache cache;
    private readonly string baseUrl;

    public CachedHttpProvider(string baseUrl, IResourceCache cache)
    {
        this.baseUrl = baseUrl;
        this.cache = cache;
        this.httpClient = new HttpClient();
    }

    public IResourceCache? GetCache() => cache;

    public async Task<bool> IsCacheStaleAsync(Resource resource)
    {
        if (cache == null) return false;
        
        if (!await cache.ExistsAsync(resource.Path))
            return true;

        // Check if remote resource is newer than cached version
        HttpResponseMessage response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, 
            $"{baseUrl}/resources/{resource.Path}"));
        
        if (response.Headers.LastModified.HasValue)
        {
            DateTimeOffset? cachedTime = await cache.GetCreationTimeAsync(resource.Path);
            return cachedTime == null || cachedTime < response.Headers.LastModified.Value;
        }

        return false;
    }

    public async Task UpdateCacheAsync(Resource resource)
    {
        if (cache == null) return;

        HttpResponseMessage response = await httpClient.GetAsync($"{baseUrl}/resources/{resource.Path}");
        using (Stream stream = await response.Content.ReadAsStreamAsync())
        {
            await cache.StoreAsync(resource.Path, stream);
        }
    }

    public async Task<bool> ResourceExistsAsync(Resource resource)
    {
        // Check cache first
        if (cache != null && await cache.ExistsAsync(resource.Path))
        {
            if (!await IsCacheStaleAsync(resource))
                return true;
        }

        // Check remote
        try
        {
            HttpResponseMessage response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, 
                $"{baseUrl}/resources/{resource.Path}"));
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Stream> GetResourceStreamAsync(Resource resource)
    {
        // Check cache first
        if (cache != null && await cache.ExistsAsync(resource.Path))
        {
            if (!await IsCacheStaleAsync(resource))
                return await cache.GetStreamAsync(resource.Path);
        }

        // Download from remote
        HttpResponseMessage response = await httpClient.GetAsync($"{baseUrl}/resources/{resource.Path}");
        
        if (!response.IsSuccessStatusCode)
            throw new FileNotFoundException($"Resource '{resource.Path}' not found.");

        Stream stream = await response.Content.ReadAsStreamAsync();

        // Cache the result
        if (cache != null)
        {
            await cache.StoreAsync(resource.Path, stream);
            stream.Position = 0; // Reset for reading
        }

        return stream;
    }
}
```

## Cache Implementation Strategies

### File System Cache

The library includes a `FileSystemCache` implementation that stores cached resources on disk:

```csharp
FileSystemCache cache = new FileSystemCache("/path/to/cache/directory");
CachedHttpProvider provider = new CachedHttpProvider("https://api.example.com", cache);
```

### Custom Cache Implementation

You can implement your own cache by implementing `IResourceCache`:

```csharp
public class MemoryCache : IResourceCache
{
    private readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>();
    private readonly Dictionary<string, DateTimeOffset> timestamps = new Dictionary<string, DateTimeOffset>();

    public Task<bool> ExistsAsync(string resourcePath)
    {
        return Task.FromResult(cache.ContainsKey(resourcePath));
    }

    public Task<Stream> GetStreamAsync(string resourcePath)
    {
        if (!cache.ContainsKey(resourcePath))
            throw new FileNotFoundException($"Resource '{resourcePath}' not found in cache.");

        return Task.FromResult<Stream>(new MemoryStream(cache[resourcePath]));
    }

    public Task<DateTimeOffset?> GetCreationTimeAsync(string resourcePath)
    {
        if (timestamps.TryGetValue(resourcePath, out DateTimeOffset timestamp))
            return Task.FromResult<DateTimeOffset?>(timestamp);
        
        return Task.FromResult<DateTimeOffset?>(null);
    }

    public async Task StoreAsync(string resourcePath, Stream content)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            await content.CopyToAsync(ms);
            cache[resourcePath] = ms.ToArray();
            timestamps[resourcePath] = DateTimeOffset.UtcNow;
        }
    }

    public Task StoreAsync(string resourcePath, byte[] content)
    {
        cache[resourcePath] = content;
        timestamps[resourcePath] = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task StoreAsync(string resourcePath, string content)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return StoreAsync(resourcePath, bytes);
    }
}
```

## Cache Invalidation Strategies

### Time-Based Invalidation

```csharp
public async Task<bool> IsCacheStaleAsync(Resource resource)
{
    if (cache == null) return false;
    
    DateTimeOffset? cachedTime = await cache.GetCreationTimeAsync(resource.Path);
    if (cachedTime == null) return true;

    // Consider cache stale after 1 hour
    TimeSpan maxAge = TimeSpan.FromHours(1);
    return DateTimeOffset.UtcNow - cachedTime.Value > maxAge;
}
```

### ETag-Based Invalidation

```csharp
public async Task<bool> IsCacheStaleAsync(Resource resource)
{
    if (cache == null) return false;
    
    // Get cached ETag
    string? cachedETag = await GetCachedETagAsync(resource.Path);
    if (cachedETag == null) return true;

    // Check current ETag from server
    HttpResponseMessage response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, 
        $"{baseUrl}/resources/{resource.Path}"));
    
    string? currentETag = response.Headers.ETag?.Tag;
    return currentETag != cachedETag;
}
```

### Version-Based Invalidation

```csharp
public async Task<bool> IsCacheStaleAsync(Resource resource)
{
    if (cache == null) return false;
    
    // Get cached version
    string? cachedVersion = await GetCachedVersionAsync(resource.Path);
    if (cachedVersion == null) return true;

    // Check current version from server
    string currentVersion = await GetCurrentVersionAsync(resource.Path);
    return currentVersion != cachedVersion;
}
```

## Best Practices

### 1. Graceful Degradation

Always handle cache failures gracefully:

```csharp
public async Task<Stream> GetResourceStreamAsync(Resource resource)
{
    try
    {
        // Try cache first
        if (cache != null && await cache.ExistsAsync(resource.Path))
        {
            if (!await IsCacheStaleAsync(resource))
                return await cache.GetStreamAsync(resource.Path);
        }
    }
    catch (Exception ex)
    {
        // Log cache error but continue with remote fetch
        Console.WriteLine($"Cache error for {resource.Path}: {ex.Message}");
    }

    // Fall back to remote fetch
    return await FetchFromRemote(resource);
}
```





## Summary

Caching in resource providers can significantly improve performance and reduce network traffic. Key points:

1. **Implement `ICacheableResourceProvider`** - Add caching capabilities to your providers
2. **Choose appropriate invalidation strategy** - Time-based, ETag-based, or version-based
3. **Handle cache failures gracefully** - Don't let cache errors break your application
4. **Manage cache size** - Prevent memory/disk exhaustion
5. **Test thoroughly** - Ensure caching works correctly in all scenarios

Caching transforms resource providers from simple data access layers into intelligent, performance-optimized components that can work offline and reduce load on remote systems. 