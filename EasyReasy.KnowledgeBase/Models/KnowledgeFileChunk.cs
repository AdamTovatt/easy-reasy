namespace EasyReasy.KnowledgeBase.Models
{
    /// <summary>
    /// Represents a chunk of content from a knowledge file.
    /// </summary>
    public class KnowledgeFileChunk
    {
        /// <summary>
        /// Gets or sets the unique identifier for the chunk.
        /// </summary>
        public Guid Id { get; set; }

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
        /// <param name="content">The text content of the chunk.</param>
        /// <param name="embedding">The embedding vector for the chunk.</param>
        public KnowledgeFileChunk(Guid id, string content, float[]? embedding = null)
        {
            Id = id;
            Content = content;
            Embedding = embedding;
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
