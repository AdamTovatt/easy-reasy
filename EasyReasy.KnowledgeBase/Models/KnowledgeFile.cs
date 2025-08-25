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
        /// Gets or sets the date and time when the file was processed.
        /// </summary>
        public DateTime ProcessedAt { get; set; }

        /// <summary>
        /// Gets or sets the current indexing status of the knowledge file.
        /// </summary>
        public IndexingStatus Status { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KnowledgeFile"/> class with default values for processed time and status.
        /// </summary>
        /// <param name="id">The unique identifier for the knowledge file.</param>
        /// <param name="name">The name of the knowledge file.</param>
        /// <param name="hash">The hash of the file content.</param>
        public KnowledgeFile(Guid id, string name, byte[] hash)
            : this(id, name, hash, DateTime.UtcNow, IndexingStatus.Pending)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KnowledgeFile"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the knowledge file.</param>
        /// <param name="name">The name of the knowledge file.</param>
        /// <param name="hash">The hash of the file content.</param>
        /// <param name="processedAt">The date and time when the file was processed.</param>
        /// <param name="status">The current indexing status of the knowledge file.</param>
        public KnowledgeFile(Guid id, string name, byte[] hash, DateTime processedAt, IndexingStatus status)
        {
            Id = id;
            Name = name;
            Hash = hash;
            ProcessedAt = processedAt;
            Status = status;
        }
    }
}
