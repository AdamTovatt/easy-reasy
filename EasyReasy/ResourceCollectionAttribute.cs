namespace EasyReasy
{
    /// <summary>
    /// Marks a class as a resource collection and specifies the provider type to use for accessing its resources.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ResourceCollectionAttribute : Attribute
    {
        /// <summary>
        /// Gets the type of provider to use for this resource collection.
        /// </summary>
        public Type ProviderType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceCollectionAttribute"/> class.
        /// </summary>
        /// <param name="providerType">The type of provider to use for accessing resources in this collection.</param>
        public ResourceCollectionAttribute(Type providerType)
        {
            ArgumentNullException.ThrowIfNull(providerType);

            ProviderType = providerType;
        }
    }
}