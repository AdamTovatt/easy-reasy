namespace EasyReasy
{
    /// <summary>
    /// Defines the interface for caching resources.
    /// </summary>
    public interface IResourceCache
    {
        /// <summary>
        /// Checks if a resource exists in the cache.
        /// </summary>
        /// <param name="resourcePath">The path of the resource to check.</param>
        /// <returns>True if the resource exists in the cache; otherwise, false.</returns>
        Task<bool> ExistsAsync(string resourcePath);

        /// <summary>
        /// Gets a stream for reading a cached resource.
        /// </summary>
        /// <param name="resourcePath">The path of the resource to read.</param>
        /// <returns>A stream for reading the cached resource.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the resource is not found in the cache.</exception>
        Task<Stream> GetStreamAsync(string resourcePath);

        /// <summary>
        /// Gets the creation time of a cached resource.
        /// </summary>
        /// <param name="resourcePath">The path of the resource to check.</param>
        /// <returns>The creation time of the cached resource, or null if the resource doesn't exist.</returns>
        Task<DateTimeOffset?> GetCreationTimeAsync(string resourcePath);

        /// <summary>
        /// Stores a resource in the cache.
        /// </summary>
        /// <param name="resourcePath">The path of the resource to store.</param>
        /// <param name="content">The content stream to cache.</param>
        /// <returns>A task that represents the asynchronous cache operation.</returns>
        Task StoreAsync(string resourcePath, Stream content);

        /// <summary>
        /// Stores a resource in the cache as a byte array.
        /// </summary>
        /// <param name="resourcePath">The path of the resource to store.</param>
        /// <param name="content">The content bytes to cache.</param>
        /// <returns>A task that represents the asynchronous cache operation.</returns>
        async Task StoreAsync(string resourcePath, byte[] content)
        {
            using (MemoryStream memoryStream = new MemoryStream(content, writable: false))
            {
                await StoreAsync(resourcePath, memoryStream);
            }
        }

        /// <summary>
        /// Stores a resource in the cache as a string.
        /// </summary>
        /// <param name="resourcePath">The path of the resource to store.</param>
        /// <param name="content">The content string to cache.</param>
        /// <returns>A task that represents the asynchronous cache operation.</returns>
        async Task StoreAsync(string resourcePath, string content)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
            await StoreAsync(resourcePath, bytes);
        }
    }
}