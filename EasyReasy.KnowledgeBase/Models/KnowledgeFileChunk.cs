using EasyReasy.KnowledgeBase.ConfidenceRating;

namespace EasyReasy.KnowledgeBase.Models
{
    /// <summary>
    /// Represents a chunk of content from a knowledge file.
    /// </summary>
    public class KnowledgeFileChunk : IVectorObject
    {
        /// <summary>
        /// Gets or sets the unique identifier for the chunk.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the section this chunk belongs to.
        /// </summary>
        public Guid SectionId { get; set; }

        /// <summary>
        /// Gets or sets the zero-based index of this chunk within the section.
        /// </summary>
        public int ChunkIndex { get; set; }

        /// <summary>
        /// Gets or sets the text content of the chunk.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets the embedding vector for the chunk.
        /// </summary>
        public float[]? Embedding { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KnowledgeFileChunk"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the chunk.</param>
        /// <param name="sectionId">The unique identifier of the section this chunk belongs to.</param>
        /// <param name="chunkIndex">The zero-based index of this chunk within the section.</param>
        /// <param name="content">The text content of the chunk.</param>
        /// <param name="embedding">The embedding vector for the chunk.</param>
        public KnowledgeFileChunk(Guid id, Guid sectionId, int chunkIndex, string content, float[]? embedding = null)
        {
            Id = id;
            SectionId = sectionId;
            ChunkIndex = chunkIndex;
            Content = content;
            Embedding = embedding;
        }

        /// <summary>
        /// Returns the vector representation of the chunk.
        /// </summary>
        /// <returns>The embedding vector for the chunk.</returns>
        public float[] GetVector()
        {
            return Embedding ?? Array.Empty<float>();
        }

        /// <summary>
        /// Returns true if the chunk contains a valid vector.
        /// </summary>
        /// <returns>True if the embedding vector is not null.</returns>
        public bool ContainsVector()
        {
            return Embedding != null;
        }

        /// <summary>
        /// Returns the content of the chunk.
        /// </summary>
        /// <returns>The text content of the chunk.</returns>
        public override string ToString()
        {
            return Content;
        }
    }
}
