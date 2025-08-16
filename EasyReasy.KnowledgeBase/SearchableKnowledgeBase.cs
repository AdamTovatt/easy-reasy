using EasyReasy.KnowledgeBase.Chunking;
using EasyReasy.KnowledgeBase.Generation;
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
        /// <param name="targetTokenCount">The target number of tokens for the result.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A search result containing relevant knowledge base content.</returns>
        public async Task<IKnowledgeBaseSearchResult> SearchAsync(string query, int targetTokenCount, CancellationToken cancellationToken = default)
        {
            float[] queryVector = await EmbeddingService.EmbedAsync(query, cancellationToken);

            IKnowledgeVectorStore sectionsVectorStore = SearchableKnowledgeStore.GetSectionsVectorStore();

            await sectionsVectorStore.SearchAsync(queryVector, MaxSearchResultsCount);

            // TODO: Implement the complete search logic
            // use search result from sections vector store to search for chunks (I think?)
            throw new NotImplementedException("Search functionality is not yet fully implemented.");
        }
    }
}
