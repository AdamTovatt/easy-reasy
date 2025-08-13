using System.Text;

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
        public string? Summary { get; set; }

        /// <summary>
        /// Gets or sets the collection of chunks that belong to this section.
        /// </summary>
        public List<KnowledgeFileChunk> Chunks { get; set; }

        /// <summary>
        /// Gets or sets the embedding vector for the section.
        /// </summary>
        public float[]? Embedding { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KnowledgeFileSection"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the section.</param>
        /// <param name="chunks">The collection of chunks that belong to this section.</param>
        /// <param name="summary">The summary description of the section.</param>
        /// <param name="embedding">The embedding vector for the section.</param>
        public KnowledgeFileSection(Guid id, List<KnowledgeFileChunk> chunks, string? summary = null, float[]? embedding = null)
        {
            Id = id;
            Summary = summary;
            Chunks = chunks;
            Embedding = embedding;
        }

        /// <summary>
        /// Creates a new <see cref="KnowledgeFileSection"/> instance from a list of chunks, assigning a new unique identifier and no summary.
        /// </summary>
        /// <param name="chunks">The collection of <see cref="KnowledgeFileChunk"/> objects to include in the section.</param>
        /// <returns>A new <see cref="KnowledgeFileSection"/> containing the provided chunks.</returns>
        public static KnowledgeFileSection CreateFromChunks(List<KnowledgeFileChunk> chunks)
        {
            return new KnowledgeFileSection(Guid.NewGuid(), chunks);
        }

        /// <summary>
        /// Returns the combined content of all chunks in the section.
        /// </summary>
        /// <returns>The concatenated content of all chunks.</returns>
        public override string ToString()
        {
            if (Chunks == null || Chunks.Count == 0)
                return string.Empty;

            if (Chunks.Count == 1)
                return Chunks[0].Content;

            StringBuilder result = new StringBuilder();
            for (int i = 0; i < Chunks.Count; i++)
            {
                result.Append(Chunks[i].Content);
            }

            return result.ToString();
        }
    }
}
