namespace EasyReasy
{
    /// <summary>
    /// Represents a resource provider that supports caching.
    /// </summary>
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
}