namespace EasyReasy
{
    /// <summary>
    /// Defines the contract for resource providers that can access resources from different sources.
    /// </summary>
    public interface IResourceProvider
    {
        /// <summary>
        /// Checks if the specified resource exists.
        /// </summary>
        /// <param name="resource">The resource to check.</param>
        /// <returns>True if the resource exists; otherwise, false.</returns>
        Task<bool> ResourceExistsAsync(Resource resource);

        /// <summary>
        /// Gets a stream for reading the specified resource.
        /// </summary>
        /// <param name="resource">The resource to read.</param>
        /// <returns>A stream for reading the resource.</returns>
        Task<Stream> GetResourceStreamAsync(Resource resource);

        /// <summary>
        /// Reads the specified resource as a byte array.
        /// </summary>
        /// <param name="resource">The resource to read.</param>
        /// <returns>The resource content as a byte array.</returns>
        async Task<byte[]> ReadAsBytesAsync(Resource resource)
        {
            using (Stream resourceStream = await GetResourceStreamAsync(resource).ConfigureAwait(false))
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    await resourceStream.CopyToAsync(memoryStream).ConfigureAwait(false);
                    return memoryStream.ToArray();
                }
            }
        }

        /// <summary>
        /// Reads the specified resource as a string.
        /// </summary>
        /// <param name="resource">The resource to read.</param>
        /// <returns>The resource content as a string.</returns>
        async Task<string> ReadAsStringAsync(Resource resource)
        {
            using (Stream resourceStream = await GetResourceStreamAsync(resource).ConfigureAwait(false))
            {
                using (StreamReader reader = new StreamReader(resourceStream))
                {
                    return await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }
        }
    }
}