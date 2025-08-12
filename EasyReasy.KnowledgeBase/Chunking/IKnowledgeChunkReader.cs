namespace EasyReasy.KnowledgeBase.Chunking
{
    public interface IKnowledgeChunkReader
    {
        public Task<string?> ReadNextChunkContentAsync();
    }
}
