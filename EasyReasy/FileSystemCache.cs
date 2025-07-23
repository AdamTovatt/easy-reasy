namespace EasyReasy
{
    /// <summary>
    /// Implements IResourceCache using the local file system for storage.
    /// </summary>
    public class FileSystemCache : IResourceCache
    {
        /// <summary>
        /// Gets the root directory where cached files are stored.
        /// </summary>
        public string StoragePath => storagePath;

        private readonly string storagePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemCache"/> class with the specified storage path.
        /// </summary>
        /// <param name="storagePath">The root directory for cached files.</param>
        /// <exception cref="ArgumentException">Thrown if the storage path is null or whitespace.</exception>
        public FileSystemCache(string storagePath)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
                throw new ArgumentException("Storage path must not be null or whitespace.", nameof(storagePath));

            this.storagePath = storagePath;
            Directory.CreateDirectory(storagePath);
        }

        /// <summary>
        /// Gets the directory path for a given file within the cache.
        /// </summary>
        /// <param name="filePath">The relative file path within the cache.</param>
        /// <returns>The directory path for the file.</returns>
        public string GetStorageDirectoryForFile(string filePath)
        {
            string fullPath = Path.Combine(storagePath, filePath);
            return Path.GetDirectoryName(fullPath) ?? storagePath;
        }

        /// <summary>
        /// Checks if a resource exists in the cache.
        /// </summary>
        /// <param name="resourcePath">The path of the resource to check.</param>
        /// <returns>True if the resource exists; otherwise, false.</returns>
        public async Task<bool> ExistsAsync(string resourcePath)
        {
            await Task.CompletedTask;
            string filePath = GetFilePath(resourcePath);
            return File.Exists(filePath);
        }

        /// <summary>
        /// Gets a read-only stream for a cached resource.
        /// </summary>
        /// <param name="resourcePath">The path of the resource to retrieve.</param>
        /// <returns>A stream for reading the cached resource.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the resource does not exist in the cache.</exception>
        public async Task<Stream> GetStreamAsync(string resourcePath)
        {
            await Task.CompletedTask;
            string filePath = GetFilePath(resourcePath);
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Resource '{resourcePath}' not found in cache.", filePath);

            // Open as read-only, shared read
            FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return stream;
        }

        /// <summary>
        /// Gets the last write time of a cached resource.
        /// </summary>
        /// <param name="resourcePath">The path of the resource to check.</param>
        /// <returns>The last write time of the cached resource, or null if the resource doesn't exist.</returns>
        public async Task<DateTimeOffset?> GetCreationTimeAsync(string resourcePath)
        {
            await Task.CompletedTask;
            string filePath = GetFilePath(resourcePath);
            if (!File.Exists(filePath))
                return null;

            return new DateTimeOffset(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero);
        }

        /// <summary>
        /// Stores a resource in the cache from the provided stream.
        /// </summary>
        /// <param name="resourcePath">The path of the resource to store.</param>
        /// <param name="content">The stream containing the resource data.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task StoreAsync(string resourcePath, Stream content)
        {
            string filePath = GetFilePath(resourcePath);
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Overwrite if exists
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await content.CopyToAsync(fileStream);
            }
        }

        private string GetFilePath(string resourcePath)
        {
            // Normalize slashes and remove leading/trailing slashes
            string safePath = resourcePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            return Path.Combine(storagePath, safePath);
        }
    }
}