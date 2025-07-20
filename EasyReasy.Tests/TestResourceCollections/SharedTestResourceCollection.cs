using EasyReasy.Tests.TestProviders;

namespace EasyReasy.Tests.TestResourceCollections
{
    /// <summary>
    /// Shared test resource collection that uses ParameterizedFakeResourceProvider.
    /// This is used across multiple test classes to test provider registration scenarios.
    /// </summary>
    [ResourceCollection(typeof(ParameterizedFakeResourceProvider))]
    public static class SharedTestResourceCollection
    {
        public static readonly Resource TestResource = new Resource("shared-test.txt");
    }
}