using ByteShelfClient;
using ByteShelfCommon;

namespace EasyReasy.ByteShelfProvider
{
    /// <summary>
    /// Provides access to resources via ByteShelf, mapping Resource.Path directories to subtenant hierarchy.
    /// Supports optional local caching of downloaded resources.
    /// </summary>
    public class ByteShelfResourceProvider : IResourceProvider, ICacheableResourceProvider
    {
        private readonly IShelfFileProvider shelfProvider;
        private readonly string? rootSubTenantId;
        private readonly IResourceCache? cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="ByteShelfResourceProvider"/> class.
        /// </summary>
        /// <param name="baseUrl">The ByteShelf API base URL.</param>
        /// <param name="apiKey">The API key for ByteShelf.</param>
        /// <param name="rootSubTenantId">Optional root subtenant ID to start from.</param>
        /// <param name="cache">Optional cache implementation for downloaded resources.</param>
        public ByteShelfResourceProvider(
            string baseUrl,
            string apiKey,
            string? rootSubTenantId = null,
            IResourceCache? cache = null)
        {
            HttpClient httpClient = HttpShelfFileProvider.CreateHttpClient(baseUrl);
            shelfProvider = new HttpShelfFileProvider(httpClient, apiKey);
            this.rootSubTenantId = rootSubTenantId;
            this.cache = cache;
        }

        /// <summary>
        /// Gets the cache instance used by this resource provider.
        /// </summary>
        /// <returns>The cache instance, or null if no cache is configured.</returns>
        public IResourceCache? GetCache()
        {
            return cache;
        }

        /// <summary>
        /// Checks if the cached version of a resource is stale and needs to be updated.
        /// </summary>
        /// <param name="resource">The resource to check.</param>
        /// <returns>True if the cache is stale and should be updated; otherwise, false.</returns>
        public async Task<bool> IsCacheStaleAsync(Resource resource)
        {
            return await ShouldInvalidateCacheAsync(resource);
        }

        /// <summary>
        /// Updates the cache with the latest version of the resource.
        /// </summary>
        /// <param name="resource">The resource to update in cache.</param>
        /// <returns>A task that represents the asynchronous cache update operation.</returns>
        public async Task UpdateCacheAsync(Resource resource)
        {
            if (cache == null)
                return;

            // Download the latest version from ByteShelf
            (string? subTenantId, string fileName) = await ResolveSubTenantAndFileName(resource.Path);

            IEnumerable<ShelfFileMetadata> files = subTenantId == null
                ? await shelfProvider.GetFilesAsync()
                : await shelfProvider.GetFilesForTenantAsync(subTenantId);

            ShelfFileMetadata? file = files
                .Where(f => string.Equals(f.OriginalFilename, fileName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.CreatedAt)
                .FirstOrDefault();

            if (file == null)
                throw new FileNotFoundException($"File '{resource.Path}' not found in ByteShelf.");

            ShelfFile shelfFile = subTenantId == null
                ? await shelfProvider.ReadFileAsync(file.Id)
                : await shelfProvider.ReadFileForTenantAsync(subTenantId, file.Id);

            using (Stream contentStream = shelfFile.GetContentStream())
            {
                // Store the updated content in cache
                await cache.StoreAsync(resource.Path, contentStream);
            }
        }

        public async Task<bool> ResourceExistsAsync(Resource resource)
        {
            // First check if it exists in cache and is not stale
            if (cache != null)
            {
                if (await cache.ExistsAsync(resource.Path))
                {
                    // Check if we need to invalidate the cache
                    if (!await IsCacheStaleAsync(resource))
                    {
                        return true;
                    }
                    // Cache is stale, treat as non-existent and check ByteShelf below
                }
            }

            // Check if it exists in ByteShelf
            (string? subTenantId, string fileName) = await ResolveSubTenantAndFileName(resource.Path);

            IEnumerable<ShelfFileMetadata> files = subTenantId == null
                ? await shelfProvider.GetFilesAsync()
                : await shelfProvider.GetFilesForTenantAsync(subTenantId);

            // Find file by display name (OriginalFilename) - take the newest if multiple exist
            return files.Any(f => string.Equals(f.OriginalFilename, fileName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<Stream> GetResourceStreamAsync(Resource resource)
        {
            // First check if it exists in cache and is not stale
            if (cache != null)
            {
                if (await cache.ExistsAsync(resource.Path))
                {
                    // Check if we need to invalidate the cache
                    if (!await IsCacheStaleAsync(resource))
                    {
                        return await cache.GetStreamAsync(resource.Path);
                    }
                    // Cache is stale, will download fresh copy below
                }
            }

            // Download from ByteShelf and cache if cache is provided
            (string? subTenantId, string fileName) = await ResolveSubTenantAndFileName(resource.Path);

            // Find file by display name (OriginalFilename) - take the newest if multiple exist
            IEnumerable<ShelfFileMetadata> files = subTenantId == null
                ? await shelfProvider.GetFilesAsync()
                : await shelfProvider.GetFilesForTenantAsync(subTenantId);

            ShelfFileMetadata? file = files
                .Where(f => string.Equals(f.OriginalFilename, fileName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.CreatedAt)
                .FirstOrDefault();

            if (file == null)
                throw new FileNotFoundException($"File '{resource.Path}' not found in ByteShelf.");

            ShelfFile shelfFile = subTenantId == null
                ? await shelfProvider.ReadFileAsync(file.Id)
                : await shelfProvider.ReadFileForTenantAsync(subTenantId, file.Id);

            Stream contentStream = shelfFile.GetContentStream();

            // Cache the file if cache is provided
            if (cache != null)
            {
                await cache.StoreAsync(resource.Path, contentStream);
                // Reset stream position for reading
                contentStream.Position = 0;
            }

            return contentStream;
        }

        /// <summary>
        /// Resolves the subtenant hierarchy and file name from a resource path.
        /// Only traverses existing subtenants. Throws if any segment is missing.
        /// </summary>
        private async Task<(string? subTenantId, string fileName)> ResolveSubTenantAndFileName(string resourcePath)
        {
            string[] parts = resourcePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                throw new ArgumentException("Resource path is empty.", nameof(resourcePath));

            string fileName = parts[^1];

            if (parts.Length == 1)
                return (rootSubTenantId, fileName);

            // Traverse subtenant hierarchy by display name, fail if not found
            string? currentSubTenantId = rootSubTenantId;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                string segment = parts[i];

                Dictionary<string, TenantInfoResponse> subTenants = currentSubTenantId == null
                    ? await shelfProvider.GetSubTenantsAsync()
                    : await shelfProvider.GetSubTenantsUnderSubTenantAsync(currentSubTenantId);

                // Find subtenant by display name
                KeyValuePair<string, TenantInfoResponse>? found = subTenants.FirstOrDefault(st => string.Equals(st.Value.DisplayName, segment, StringComparison.OrdinalIgnoreCase));

                if (found.HasValue && !string.IsNullOrEmpty(found.Value.Key))
                {
                    currentSubTenantId = found.Value.Key;
                }
                else
                {
                    throw new DirectoryNotFoundException($"Subtenant '{segment}' not found in path '{resourcePath}'.");
                }
            }

            return (currentSubTenantId, fileName);
        }

        /// <summary>
        /// Determines if the cached version should be invalidated based on file timestamps.
        /// </summary>
        /// <param name="resource">The resource to check.</param>
        /// <returns>True if the cache should be invalidated; otherwise, false.</returns>
        private async Task<bool> ShouldInvalidateCacheAsync(Resource resource)
        {
            if (cache == null)
                return false;

            // Get cached file creation time
            DateTimeOffset? cachedTime = await cache.GetCreationTimeAsync(resource.Path);
            if (cachedTime == null)
                return false;

            try
            {
                // Get ByteShelf file metadata
                (string? subTenantId, string fileName) = await ResolveSubTenantAndFileName(resource.Path);

                IEnumerable<ShelfFileMetadata> files = subTenantId == null
                    ? await shelfProvider.GetFilesAsync()
                    : await shelfProvider.GetFilesForTenantAsync(subTenantId);

                ShelfFileMetadata? file = files
                    .Where(f => string.Equals(f.OriginalFilename, fileName, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.CreatedAt)
                    .FirstOrDefault();

                if (file == null)
                    return false;

                // Compare timestamps - if ByteShelf file is newer, invalidate cache
                return file.CreatedAt > cachedTime.Value;
            }
            catch
            {
                // If we can't determine the timestamp, assume cache is valid
                return false;
            }
        }

        /// <summary>
        /// Creates a PredefinedResourceProvider for the given resource collection type using ByteShelfResourceProvider.
        /// </summary>
        /// <param name="resourceCollectionType">The resource collection type.</param>
        /// <param name="baseUrl">The ByteShelf API base URL.</param>
        /// <param name="apiKey">The API key for ByteShelf.</param>
        /// <param name="rootSubTenantId">Optional root subtenant ID to start from.</param>
        /// <param name="cache">Optional cache implementation for downloaded resources.</param>
        /// <returns>A PredefinedResourceProvider for the specified resource collection type.</returns>
        public static PredefinedResourceProvider CreatePredefined(Type resourceCollectionType, string baseUrl, string apiKey, string? rootSubTenantId = null, IResourceCache? cache = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(baseUrl);
            ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
            ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

            ArgumentNullException.ThrowIfNull(resourceCollectionType);

            return new PredefinedResourceProvider(
                resourceCollectionType,
                new ByteShelfResourceProvider(baseUrl, apiKey, rootSubTenantId, cache)
            );
        }
    }
}