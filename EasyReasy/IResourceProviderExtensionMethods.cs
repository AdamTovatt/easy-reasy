namespace EasyReasy
{
    public static class IResourceProviderExtensionMethods
    {
        public static PredefinedResourceProvider AsPredefinedFor(this IResourceProvider resourceProvider, Type resourceCollectionType)
        {
            return new PredefinedResourceProvider(resourceCollectionType, resourceProvider);
        }
    }
}
