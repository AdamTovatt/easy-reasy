namespace EasyReasy.KnowledgeBase.ConfidenceRating
{
    /// <summary>
    /// Represents an entry of type <typeparamref name="T"/> with associated relevance metrics.
    /// </summary>
    /// <typeparam name="T">The type of the item being rated for relevance.</typeparam>
    public class RelevanceRatedEntry<T>
    {
        /// <summary>
        /// Gets the item being rated for relevance.
        /// </summary>
        public T Item { get; }

        /// <summary>
        /// Gets the relevance metrics for the item.
        /// </summary>
        public KnowledgebaseRelevanceMetrics Relevance { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RelevanceRatedEntry{T}"/> class.
        /// </summary>
        /// <param name="item">The item being rated for relevance.</param>
        /// <param name="relevance">The relevance metrics for the item.</param>
        public RelevanceRatedEntry(T item, KnowledgebaseRelevanceMetrics relevance)
        {
            Item = item;
            Relevance = relevance;
        }
    }
}