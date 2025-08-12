namespace EasyReasy.KnowledgeBase.Models
{
    public class KnowledgeFile
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public byte[] Hash { get; private set; }
    }
}
