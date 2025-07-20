namespace EasyReasy.Tests.TestProviders
{
    /// <summary>
    /// Fake resource provider specifically for provider tests.
    /// </summary>
    public class ProviderFakeResourceProvider : IResourceProvider
    {
        public async Task<bool> ResourceExistsAsync(Resource resource)
        {
            await Task.CompletedTask;
            return resource.Path == "provider-auto.txt";
        }

        public async Task<Stream> GetResourceStreamAsync(Resource resource)
        {
            await Task.CompletedTask;
            if (resource.Path == "provider-auto.txt")
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes("Test content");
                return new MemoryStream(bytes);
            }
            throw new FileNotFoundException($"Resource '{resource.Path}' not found.");
        }
    }
}