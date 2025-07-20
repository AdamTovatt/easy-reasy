namespace EasyReasy
{
    /// <summary>
    /// Associates a resource collection type with a specific IResourceProvider instance.
    /// </summary>
    public class PredefinedResourceProvider
    {
        /// <summary>
        /// Gets the resource collection type associated with this provider.
        /// </summary>
        public Type ResourceCollectionType { get; }

        /// <summary>
        /// Gets the IResourceProvider instance for the resource collection.
        /// </summary>
        public IResourceProvider Provider { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PredefinedResourceProvider"/> class.
        /// </summary>
        /// <param name="resourceCollectionType">The resource collection type.</param>
        /// <param name="provider">The provider instance to associate with the collection type.</param>
        public PredefinedResourceProvider(Type resourceCollectionType, IResourceProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(resourceCollectionType);

            ResourceCollectionType = resourceCollectionType;
            Provider = provider;
        }
    }
}