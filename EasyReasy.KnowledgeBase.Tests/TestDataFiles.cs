namespace EasyReasy.KnowledgeBase.Tests
{
    [ResourceCollection(typeof(EmbeddedResourceProvider))]
    public static class TestDataFiles
    {
        public static readonly Resource TestDocument01 = new Resource("TestData/TestDocument01.md");
    }
}
