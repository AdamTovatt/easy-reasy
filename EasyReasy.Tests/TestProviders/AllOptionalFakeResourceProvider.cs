namespace EasyReasy.Tests.TestProviders
{
    /// <summary>
    /// Provider with all-optional constructor parameters for testing auto-registration.
    /// </summary>
    public class AllOptionalFakeResourceProvider : IResourceProvider
    {
        private readonly string? _foo;
        private readonly int _bar;

        public AllOptionalFakeResourceProvider(string? foo = null, int bar = 42)
        {
            _foo = foo;
            _bar = bar;
        }

        public async Task<bool> ResourceExistsAsync(Resource resource)
        {
            await Task.CompletedTask;
            return resource.Path == "alloptional.txt";
        }

        public async Task<Stream> GetResourceStreamAsync(Resource resource)
        {
            await Task.CompletedTask;
            if (resource.Path == "alloptional.txt")
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes("All optional content");
                return new MemoryStream(bytes);
            }
            throw new FileNotFoundException($"Resource '{resource.Path}' not found.");
        }
    }
}