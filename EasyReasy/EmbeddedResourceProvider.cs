using Microsoft.Extensions.FileProviders;
using System.Reflection;

namespace EasyReasy
{
    /// <summary>
    /// Provides access to embedded resources within an assembly.
    /// </summary>
    public class EmbeddedResourceProvider : IResourceProvider
    {
        private readonly EmbeddedFileProvider fileProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddedResourceProvider"/> class.
        /// </summary>
        /// <param name="assembly">The assembly containing the embedded resources.</param>
        public EmbeddedResourceProvider(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            fileProvider = new EmbeddedFileProvider(assembly);
        }

        /// <summary>
        /// Checks if the specified resource exists in the embedded assembly.
        /// </summary>
        /// <param name="resource">The resource to check.</param>
        /// <returns>True if the resource exists; otherwise, false.</returns>
        public async Task<bool> ResourceExistsAsync(Resource resource)
        {
            await Task.CompletedTask;
            IFileInfo fileInfo = GetFileInfo(resource);
            return fileInfo.Exists;
        }

        /// <summary>
        /// Gets a stream for reading the specified embedded resource.
        /// </summary>
        /// <param name="resource">The resource to read.</param>
        /// <returns>A stream for reading the resource.</returns>
        public async Task<Stream> GetResourceStreamAsync(Resource resource)
        {
            await Task.CompletedTask;
            IFileInfo fileInfo = GetFileInfo(resource);

            if (!fileInfo.Exists)
                throw new FileNotFoundException($"Resource '{resource.Path}' not found.");

            return fileInfo.CreateReadStream();
        }

        private IFileInfo GetFileInfo(Resource resource)
        {
            string path = resource.Path;
            IFileInfo fileInfo = fileProvider.GetFileInfo(path);

            if (fileInfo.Exists)
            {
                return fileInfo;
            }

            return fileProvider.GetFileInfo($"Resources/{path}");
        }
    }
}