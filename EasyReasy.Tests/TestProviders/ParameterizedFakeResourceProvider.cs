namespace EasyReasy.Tests.TestProviders
{
    /// <summary>
    /// Fake resource provider for testing that requires constructor parameters.
    /// This is used across multiple test classes to test provider registration scenarios.
    /// </summary>
    public class ParameterizedFakeResourceProvider : IResourceProvider
    {
        private readonly string _basePath;
        private readonly bool _shouldExist;
        private readonly string _expectedResourcePath;

        public ParameterizedFakeResourceProvider(string basePath, bool shouldExist = true, string expectedResourcePath = "test.txt")
        {
            _basePath = basePath;
            _shouldExist = shouldExist;
            _expectedResourcePath = expectedResourcePath;
        }

        public async Task<bool> ResourceExistsAsync(Resource resource)
        {
            await Task.CompletedTask;
            return _shouldExist && resource.Path == _expectedResourcePath;
        }

        public async Task<Stream> GetResourceStreamAsync(Resource resource)
        {
            await Task.CompletedTask;
            if (_shouldExist && resource.Path == _expectedResourcePath)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes("Test content");
                return new MemoryStream(bytes);
            }
            throw new FileNotFoundException($"Resource '{resource.Path}' not found.");
        }
    }
}