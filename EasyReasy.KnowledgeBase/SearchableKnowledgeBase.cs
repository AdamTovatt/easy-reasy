using EasyReasy.KnowledgeBase.Chunking;
using EasyReasy.KnowledgeBase.ConfidenceRating;
using EasyReasy.KnowledgeBase.Generation;
using EasyReasy.KnowledgeBase.Indexing;
using EasyReasy.KnowledgeBase.Models;
using EasyReasy.KnowledgeBase.Searching;
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
        /// Gets or sets the tokenizer used for text processing.
        /// </summary>
        public ITokenizer Tokenizer { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of search results to return.
        /// </summary>
        public int MaxSearchResultsCount { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchableKnowledgeBase"/> class.
        /// </summary>
        /// <param name="searchableKnowledgeStore">The searchable knowledge store.</param>
        /// <param name="embeddingService">The embedding service for vector conversion.</param>
        /// <param name="tokenizer">The tokenizer for text processing.</param>
        public SearchableKnowledgeBase(ISearchableKnowledgeStore searchableKnowledgeStore, IEmbeddingService embeddingService, ITokenizer tokenizer)
        {
            SearchableKnowledgeStore = searchableKnowledgeStore;
            EmbeddingService = embeddingService;
            Tokenizer = tokenizer;
            MaxSearchResultsCount = 10;
        }

        /// <summary>
        /// Creates an indexer that can be used to add documents to this knowledge base.
        /// </summary>
        /// <param name="customEmbeddingService">Optional custom embedding service to use for indexing. If not provided, uses the default embedding service.</param>
        /// <returns>An indexer instance that can consume file sources.</returns>
        public IIndexer CreateIndexer(IEmbeddingService? customEmbeddingService = null)
        {
            IEmbeddingService embeddingServiceToUse = customEmbeddingService ?? EmbeddingService;
            return new KnowledgeBaseIndexer(SearchableKnowledgeStore, embeddingServiceToUse, Tokenizer);
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

                // Get all chunk IDs from the search results
                IEnumerable<Guid> chunkIds = chunkResults.Select(cv => cv.Id);

                // Retrieve all chunks in a single batch operation
                IEnumerable<KnowledgeFileChunk> chunks = await SearchableKnowledgeStore.Chunks.GetAsync(chunkIds);

                // Create a dictionary for quick lookup of chunks by ID
                Dictionary<Guid, KnowledgeFileChunk> chunksById = chunks.ToDictionary(c => c.Id);

                // Create WithSimilarity objects for chunks that contain vectors
                foreach (IKnowledgeVector chunkVector in chunkResults)
                {
                    if (chunksById.TryGetValue(chunkVector.Id, out KnowledgeFileChunk? chunk) && chunk.ContainsVector())
                    {
                        // Use the existing WithSimilarity infrastructure
                        WithSimilarity<KnowledgeFileChunk> chunkWithSimilarity = WithSimilarity<KnowledgeFileChunk>.CreateBetween(chunk, queryVector);
                        chunksWithSimilarity.Add(chunkWithSimilarity);
                    }
                }

                // 3. Group chunks by section
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
                    relevantSections: relevantSections
                        .OrderByDescending(r => r.Relevance.CosineSimilarity)
                        .ThenByDescending(r => r.Relevance.NormalizedScore)
                        .ToList(),
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
            // Guards
            if (sectionChunks.Count == 0 || section.Chunks.Count == 0)
            {
                return new KnowledgebaseRelevanceMetrics(
                    cosineSimilarity: 0.0,
                    relevanceScore: 0,
                    normalizedScore: 0.0,
                    standardDeviation: 0.0);
            }

            int sectionTotalChunks = section.Chunks.Count;
            int sectionHitChunks = sectionChunks.Count;

            // Clamp similarities and cache section data
            double sumSim = 0.0;
            for (int i = 0; i < sectionHitChunks; i++)
            {
                double s = sectionChunks[i].Similarity;
                if (s < 0.0) s = 0.0;
                else if (s > 1.0) s = 1.0;
                sectionChunks[i] = new WithSimilarity<KnowledgeFileChunk>(sectionChunks[i].Item, s);
                sumSim += s;
            }

            double[] sectionSimilarities = sectionChunks.Select(c => c.Similarity).ToArray();
            double[] allSimilarities = allChunks.Select(c => Math.Clamp(c.Similarity, 0.0, 1.0)).ToArray();

            // Core signals
            double maxSim = sectionSimilarities.Max();
            int k = Math.Min(3, sectionSimilarities.Length);
            double meanTopK = sectionSimilarities.OrderByDescending(s => s).Take(k).Average();

            // Coverage: mean similarity across the whole section (missing chunks count as 0), then dampen
            double meanOverSection = sumSim / (double)Math.Max(1, sectionTotalChunks);
            double coverage = Math.Sqrt(meanOverSection);

            // Stable normalization (z-score over this result set)
            double globalMean = ConfidenceMath.CalculateMean(allSimilarities);
            double globalStd = Math.Max(ConfidenceMath.CalculateStandardDeviation(allSimilarities), 1e-12);
            double meanZ = sectionSimilarities.Select(s => (s - globalMean) / globalStd).Average();
            double normalizedScore = (1.0 / (1.0 + Math.Exp(-meanZ))) * 100.0;

            // Composite score (weights sum to 1.0)
            double composite = (0.55 * maxSim) + (0.35 * meanTopK) + (0.10 * coverage);

            return new KnowledgebaseRelevanceMetrics(
                cosineSimilarity: composite,
                relevanceScore: ConfidenceMath.RoundToInt(composite * 100.0),
                normalizedScore: normalizedScore,
                standardDeviation: globalStd);
        }
    }
}
