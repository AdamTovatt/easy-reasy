namespace EasyReasy.KnowledgeBase.Models
{
    /// <summary>
    /// Represents a section of a knowledge file containing a summary and associated chunks.
    /// </summary>
    public class KnowledgeFileSection
    {
        /// <summary>
        /// Gets or sets the unique identifier for the section.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the summary description of the section.
        /// </summary>
        public required string Summary { get; set; }

        /// <summary>
        /// Gets or sets the collection of chunks that belong to this section.
        /// </summary>
        public List<KnowledgeFileChunk> Chunks { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KnowledgeFileSection"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the section.</param>
        /// <param name="summary">The summary description of the section.</param>
        /// <param name="chunks">The collection of chunks that belong to this section.</param>
        public KnowledgeFileSection(Guid id, string summary, List<KnowledgeFileChunk> chunks)
        {
            Id = id;
            Summary = summary;
            Chunks = chunks;
        }
    }
}
