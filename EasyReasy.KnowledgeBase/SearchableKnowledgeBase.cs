using EasyReasy.KnowledgeBase.ConfidenceRating;
using EasyReasy.KnowledgeBase.Generation;
using EasyReasy.KnowledgeBase.Models;
using EasyReasy.KnowledgeBase.Searching;
using EasyReasy.KnowledgeBase.Searchings;
using EasyReasy.KnowledgeBase.Storage;

namespace EasyReasy.KnowledgeBase
{
    /// <summary>
    /// Provides search functionality for a knowledge base using vector embeddings.
    /// </summary>
    public class SearchableKnowledgeBase : ISearchableKnowledgeBase
    {
        /// <summary>
        /// Gets or sets the searchable knowledge store used for retrieving data.
        /// </summary>
        public ISearchableKnowledgeStore SearchableKnowledgeStore { get; set; }

        /// <summary>
        /// Gets or sets the embedding service used to convert queries to vectors.
        /// </summary>
        public IEmbeddingService EmbeddingService { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of search results to return.
        /// </summary>
        public int MaxSearchResultsCount { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchableKnowledgeBase"/> class.
        /// </summary>
        /// <param name="searchableKnowledgeStore">The searchable knowledge store.</param>
        /// <param name="embeddingService">The embedding service for vector conversion.</param>
        public SearchableKnowledgeBase(ISearchableKnowledgeStore searchableKnowledgeStore, IEmbeddingService embeddingService)
        {
            SearchableKnowledgeStore = searchableKnowledgeStore;
            EmbeddingService = embeddingService;
            MaxSearchResultsCount = 10;
        }

        /// <summary>
        /// Searches the knowledge base for content relevant to the query.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="maxSearchResultsCount">The maximum number of search results to return.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A search result containing relevant knowledge base content.</returns>
        public async Task<IKnowledgeBaseSearchResult> SearchAsync(string query, int? maxSearchResultsCount = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // 1. Search chunks directly using vector similarity
                float[] queryVector = await EmbeddingService.EmbedAsync(query, cancellationToken);
                IKnowledgeVectorStore chunksVectorStore = SearchableKnowledgeStore.GetChunksVectorStore();
                IEnumerable<IKnowledgeVector> chunkResults = await chunksVectorStore.SearchAsync(queryVector, maxSearchResultsCount ?? MaxSearchResultsCount);

                // 2. Get actual chunks and create WithSimilarity objects
                List<WithSimilarity<KnowledgeFileChunk>> chunksWithSimilarity = new List<WithSimilarity<KnowledgeFileChunk>>();
                foreach (IKnowledgeVector chunkVector in chunkResults)
                {
                    KnowledgeFileChunk? chunk = await SearchableKnowledgeStore.Chunks.GetAsync(chunkVector.Id);
                    if (chunk != null && chunk.ContainsVector())
                    {
                        // Use the existing WithSimilarity infrastructure
                        WithSimilarity<KnowledgeFileChunk> chunkWithSimilarity = WithSimilarity<KnowledgeFileChunk>.CreateBetween(chunk, queryVector);
                        chunksWithSimilarity.Add(chunkWithSimilarity);
                    }
                }

                // 3. Group chunks by section and calculate section-level relevance
                Dictionary<Guid, List<WithSimilarity<KnowledgeFileChunk>>> sectionsByChunks = chunksWithSimilarity
                    .GroupBy(c => c.Item.SectionId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // 4. Get complete sections and calculate relevance metrics
                List<RelevanceRatedEntry<KnowledgeFileSection>> relevantSections = new List<RelevanceRatedEntry<KnowledgeFileSection>>();
                foreach (KeyValuePair<Guid, List<WithSimilarity<KnowledgeFileChunk>>> sectionGroup in sectionsByChunks)
                {
                    KnowledgeFileSection? section = await SearchableKnowledgeStore.Sections.GetAsync(sectionGroup.Key);
                    if (section != null)
                    {
                        // Calculate section relevance using the confidence rating system
                        KnowledgebaseRelevanceMetrics sectionRelevance = CalculateSectionRelevanceMetrics(section, sectionGroup.Value, chunksWithSimilarity);
                        relevantSections.Add(new RelevanceRatedEntry<KnowledgeFileSection>(section, sectionRelevance));
                    }
                }

                return new KnowledgeBaseSearchResult(
                    relevantSections: relevantSections.OrderByDescending(r => r.Relevance.NormalizedScore).ToList(),
                    query: query);
            }
            catch (Exception ex)
            {
                return KnowledgeBaseSearchResult.CreateError(query, ex.Message, canBeRetried: true);
            }
        }

        /// <summary>
        /// Calculates relevance metrics for a section based on its chunks' similarity scores.
        /// </summary>
        /// <param name="section">The section to calculate relevance for.</param>
        /// <param name="sectionChunks">The chunks from this section with their similarity scores.</param>
        /// <param name="allChunks">All chunks with similarity scores for normalization.</param>
        /// <returns>Relevance metrics for the section.</returns>
        private KnowledgebaseRelevanceMetrics CalculateSectionRelevanceMetrics(
            KnowledgeFileSection section,
            List<WithSimilarity<KnowledgeFileChunk>> sectionChunks,
            List<WithSimilarity<KnowledgeFileChunk>> allChunks)
        {
            // Extract similarity scores for this section's chunks
            double[] sectionSimilarities = sectionChunks.Select(c => c.Similarity).ToArray();

            // Extract all similarity scores for normalization
            double[] allSimilarities = allChunks.Select(c => c.Similarity).ToArray();

            // Calculate metrics using ConfidenceMath
            double maxSimilarity = sectionSimilarities.Max();
            double avgSimilarity = ConfidenceMath.CalculateMean(sectionSimilarities);
            double standardDeviation = ConfidenceMath.CalculateStandardDeviation(allSimilarities);

            // Normalize scores to 0-100 range
            double minSimilarity = allSimilarities.Min();
            double maxOverallSimilarity = allSimilarities.Max();
            double[] normalizedScores = ConfidenceMath.MinMaxNormalization(sectionSimilarities, minSimilarity, maxOverallSimilarity);
            double normalizedScore = ConfidenceMath.CalculateMean(normalizedScores);

            // Use the best similarity as the primary metric, but consider coverage
            double coverageFactor = (double)sectionChunks.Count / section.Chunks.Count;
            double finalSimilarity = maxSimilarity * coverageFactor;

            return new KnowledgebaseRelevanceMetrics(
                cosineSimilarity: finalSimilarity,
                relevanceScore: ConfidenceMath.RoundToInt(finalSimilarity * 100),
                normalizedScore: normalizedScore,
                standardDeviation: standardDeviation);
        }
    }
}
