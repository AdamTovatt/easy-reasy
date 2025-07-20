using System.Reflection;

namespace EasyReasy.Tests.TestProviders
{
    /// <summary>
    /// Provider that stores the assembly it receives in its constructor for testing assembly injection.
    /// </summary>
    public class AssemblyAwareTestProvider : IResourceProvider
    {
        public static Assembly? LastReceivedAssembly { get; private set; }

        public AssemblyAwareTestProvider(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            LastReceivedAssembly = assembly;
        }

        public async Task<bool> ResourceExistsAsync(Resource resource)
        {
            await Task.CompletedTask;
            return resource.Path == "assemblyaware.txt";
        }

        public async Task<Stream> GetResourceStreamAsync(Resource resource)
        {
            await Task.CompletedTask;
            if (resource.Path == "assemblyaware.txt")
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes("Assembly aware content");
                return new MemoryStream(bytes);
            }
            throw new FileNotFoundException($"Resource '{resource.Path}' not found.");
        }
    }
}