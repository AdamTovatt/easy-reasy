namespace EasyReasy.KnowledgeBase.ConfidenceRating
{
    /// <summary>
    /// Represents relevance metrics for a search result (document or chunk).
    /// </summary>
    public class KnowledgebaseRelevanceMetrics
    {
        /// <summary>
        /// Gets the raw cosine similarity (e.g., 0.82).
        /// </summary>
        public double CosineSimilarity { get; }

        /// <summary>
        /// Gets the relevance score (e.g., 82 for 0.82 similarity).
        /// </summary>
        public int RelevanceScore { get; }

        /// <summary>
        /// Gets the normalized score (0-100 scale, based on min/max in result set).
        /// </summary>
        public double NormalizedScore { get; }

        /// <summary>
        /// Gets the standard deviation of the top-k similarity scores.
        /// </summary>
        public double StandardDeviation { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KnowledgebaseRelevanceMetrics"/> class.
        /// </summary>
        /// <param name="cosineSimilarity">The raw cosine similarity value.</param>
        /// <param name="relevanceScore">The relevance score.</param>
        /// <param name="normalizedScore">The normalized score on a 0-100 scale.</param>
        /// <param name="standardDeviation">The standard deviation of the top-k similarity scores.</param>
        public KnowledgebaseRelevanceMetrics(double cosineSimilarity, int relevanceScore, double normalizedScore, double standardDeviation)
        {
            CosineSimilarity = cosineSimilarity;
            RelevanceScore = relevanceScore;
            NormalizedScore = normalizedScore;
            StandardDeviation = standardDeviation;
        }
    }
}