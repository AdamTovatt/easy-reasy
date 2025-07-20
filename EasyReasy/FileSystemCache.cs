namespace EasyReasy
{
    /// <summary>
    /// Implements IResourceCache using the local file system for storage.
    /// </summary>
    public class FileSystemCache : IResourceCache
    {
        public string StoragePath => storagePath;

        private readonly string storagePath;

        public FileSystemCache(string storagePath)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
                throw new ArgumentException("Storage path must not be null or whitespace.", nameof(storagePath));

            this.storagePath = storagePath;
            Directory.CreateDirectory(storagePath);
        }

        public string GetStorageDirectoryForFile(string filePath)
        {
            string fullPath = Path.Combine(storagePath, filePath);
            return Path.GetDirectoryName(fullPath) ?? storagePath;
        }

        public async Task<bool> ExistsAsync(string resourcePath)
        {
            await Task.CompletedTask;
            string filePath = GetFilePath(resourcePath);
            return File.Exists(filePath);
        }

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