namespace EasyReasy.Tests.TestProviders
{
    /// <summary>
    /// Simple fake resource provider for testing (no constructor parameters).
    /// This is used across multiple test classes to test auto-registration scenarios.
    /// </summary>
    public class FakeResourceProvider : IResourceProvider
    {
        private readonly string[] _supportedPaths;

        public FakeResourceProvider(params string[] supportedPaths)
        {
            _supportedPaths = supportedPaths.Length > 0 ? supportedPaths : new[] { "test.txt" };
        }

        public async Task<bool> ResourceExistsAsync(Resource resource)
        {
            await Task.CompletedTask;
            return _supportedPaths.Contains(resource.Path);
        }

        public async Task<Stream> GetResourceStreamAsync(Resource resource)
        {
            await Task.CompletedTask;
            if (_supportedPaths.Contains(resource.Path))
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes("Test content");
                return new MemoryStream(bytes);
            }
            throw new FileNotFoundException($"Resource '{resource.Path}' not found.");
        }
    }
}