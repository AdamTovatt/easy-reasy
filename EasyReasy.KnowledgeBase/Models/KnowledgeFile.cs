namespace EasyReasy.KnowledgeBase.Models
{
    /// <summary>
    /// Represents a knowledge file with metadata including ID, name, and content hash.
    /// </summary>
    public class KnowledgeFile
    {
        /// <summary>
        /// Gets or sets the unique identifier for the knowledge file.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the knowledge file.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the hash of the file content for integrity verification.
        /// </summary>
        public byte[] Hash { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KnowledgeFile"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the knowledge file.</param>
        /// <param name="name">The name of the knowledge file.</param>
        /// <param name="hash">The hash of the file content.</param>
        public KnowledgeFile(Guid id, string name, byte[] hash)
        {
            Id = id;
            Name = name;
            Hash = hash;
        }
    }
}
