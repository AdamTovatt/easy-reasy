namespace EasyReasy.KnowledgeBase.Models
{
    public class KnowledgeFile
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public byte[] Hash { get; set; }

        public KnowledgeFile(Guid id, string name, byte[] hash)
        {
            Id = id;
            Name = name;
            Hash = hash;
        }
    }
}
