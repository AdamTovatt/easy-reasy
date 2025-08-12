namespace EasyReasy.KnowledgeBase.Models
{
    public class KnowledgeFile
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public required byte[] Hash { get; set; }
    }
}
