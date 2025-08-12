namespace EasyReasy.KnowledgeBase.Models
{
    public class KnowledgeFileSection
    {
        public Guid Id { get; set; }
        public required string Summary { get; set; }
        public List<KnowledgeFileChunk> Chunks { get; set; }

        public KnowledgeFileSection(Guid id, string summary, List<KnowledgeFileChunk> chunks)
        {
            Id = id;
            Summary = summary;
            Chunks = chunks;
        }
    }
}
