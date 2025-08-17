using EasyReasy.KnowledgeBase.Chunking;
using EasyReasy.KnowledgeBase.Generation;
using EasyReasy.KnowledgeBase.Models;
using EasyReasy.KnowledgeBase.Searching;

namespace EasyReasy.KnowledgeBase.Indexing
{
    /// <summary>
    /// An indexer that processes file sources and adds them to a knowledge base.
    /// </summary>
    public class KnowledgeBaseIndexer : IIndexer
    {
        private readonly ISearchableKnowledgeStore _searchableKnowledgeStore;
        private readonly IEmbeddingService _embeddingService;
        private readonly ITokenizer _tokenizer;
        private readonly int _maxTokensPerChunk;
        private readonly int _maxTokensPerSection;

        /// <summary>
        /// Initializes a new instance of the <see cref="KnowledgeBaseIndexer"/> class.
        /// </summary>
        /// <param name="searchableKnowledgeStore">The searchable knowledge store to add content to.</param>
        /// <param name="embeddingService">The embedding service to generate embeddings.</param>
        /// <param name="tokenizer">The tokenizer to use for text processing.</param>
        /// <param name="maxTokensPerChunk">The maximum number of tokens per chunk.</param>
        /// <param name="maxTokensPerSection">The maximum number of tokens per section.</param>
        public KnowledgeBaseIndexer(
            ISearchableKnowledgeStore searchableKnowledgeStore,
            IEmbeddingService embeddingService,
            ITokenizer tokenizer,
            int maxTokensPerChunk = 100,
            int maxTokensPerSection = 1000)
        {
            _searchableKnowledgeStore = searchableKnowledgeStore ?? throw new ArgumentNullException(nameof(searchableKnowledgeStore));
            _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _maxTokensPerChunk = maxTokensPerChunk;
            _maxTokensPerSection = maxTokensPerSection;
        }

        /// <summary>
        /// Consumes a file source and indexes its content into the knowledge base.
        /// </summary>
        /// <param name="fileSource">The file source to index.</param>
        /// <returns>A task that represents the asynchronous indexing operation.</returns>
        public async Task ConsumeAsync(IFileSource fileSource)
        {
            if (fileSource == null)
            {
                throw new ArgumentNullException(nameof(fileSource));
            }

            // Generate hash from the file content
            byte[] fileHash;
            using (Stream hashStream = fileSource.CreateReadStream())
            {
                fileHash = StreamHashHelper.GenerateSha256Hash(hashStream);
            }

            // Check if file already exists with the same hash
            KnowledgeFile? existingFile = await _searchableKnowledgeStore.Files.GetAsync(fileSource.FileId);

            if (existingFile != null && existingFile.Hash.SequenceEqual(fileHash))
            {
                // File already indexed with the same hash, skip processing
                return;
            }

            // If file exists but hash is different, remove the old content
            if (existingFile != null)
            {
                await RemoveExistingFileContentAsync(fileSource.FileId);
            }
            else // File doesn't exist
            {
                // Add the new file to the knowledge store
                KnowledgeFile knowledgeFile = new KnowledgeFile(fileSource.FileId, fileSource.FileName, fileHash);
                await _searchableKnowledgeStore.Files.AddAsync(knowledgeFile);
            }

            // Create section reader and process the file
            using (Stream contentStream = fileSource.CreateReadStream())
            {
                SectionReaderFactory factory = new SectionReaderFactory(_embeddingService, _tokenizer);
                SectionReader sectionReader = factory.CreateForMarkdown(contentStream, fileSource.FileId, _maxTokensPerChunk, _maxTokensPerSection);

                Storage.IKnowledgeVectorStore chunkVectorStore = _searchableKnowledgeStore.GetChunksVectorStore();

                int sectionIndex = 0;
                await foreach (List<KnowledgeFileChunk> chunks in sectionReader.ReadSectionsAsync())
                {
                    KnowledgeFileSection section = KnowledgeFileSection.CreateFromChunks(chunks, fileSource.FileId, sectionIndex);
                    await _searchableKnowledgeStore.Sections.AddAsync(section);

                    foreach (KnowledgeFileChunk chunk in chunks)
                    {
                        await _searchableKnowledgeStore.Chunks.AddAsync(chunk);

                        if (chunk.Embedding == null)
                        {
                            throw new InvalidOperationException($"Chunk {chunk.Id} has no embedding generated.");
                        }

                        await chunkVectorStore.AddAsync(chunk.Id, chunk.Embedding);
                    }

                    sectionIndex++;
                }
            }
        }

        private async Task RemoveExistingFileContentAsync(Guid fileId)
        {
            // Delete all chunks and sections for this file using the bulk delete methods
            await _searchableKnowledgeStore.Chunks.DeleteByFileAsync(fileId);
            await _searchableKnowledgeStore.Sections.DeleteByFileAsync(fileId);
        }
    }
}
