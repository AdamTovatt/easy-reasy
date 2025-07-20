namespace EasyReasy.Tests.TestProviders
{
    /// <summary>
    /// Fake resource provider specifically for auto-registration tests.
    /// </summary>
    public class AutoRegistrationFakeResourceProvider : IResourceProvider
    {
        public async Task<bool> ResourceExistsAsync(Resource resource)
        {
            await Task.CompletedTask;
            return resource.Path == "auto-registration-auto.txt";
        }

        public async Task<Stream> GetResourceStreamAsync(Resource resource)
        {
            await Task.CompletedTask;
            if (resource.Path == "auto-registration-auto.txt")
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes("Test content");
                return new MemoryStream(bytes);
            }
            throw new FileNotFoundException($"Resource '{resource.Path}' not found.");
        }
    }
}