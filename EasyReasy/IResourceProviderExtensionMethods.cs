namespace EasyReasy
{
    /// <summary>
    /// Extension methods for <see cref="IResourceProvider"/>.
    /// </summary>
    public static class IResourceProviderExtensionMethods
    {
        /// <summary>
        /// Wraps an <see cref="IResourceProvider"/> as a <see cref="PredefinedResourceProvider"/> for a specific resource collection type.
        /// </summary>
        /// <param name="resourceProvider">The resource provider to wrap.</param>
        /// <param name="resourceCollectionType">The resource collection type to associate with the provider.</param>
        /// <returns>A <see cref="PredefinedResourceProvider"/> instance.</returns>
        public static PredefinedResourceProvider AsPredefinedFor(this IResourceProvider resourceProvider, Type resourceCollectionType)
        {
            return new PredefinedResourceProvider(resourceCollectionType, resourceProvider);
        }
    }
}
