using EasyReasy.KnowledgeBase.ConfidenceRating;
using EasyReasy.KnowledgeBase.Generation;
using EasyReasy.KnowledgeBase.Models;
using System.Runtime.CompilerServices;

namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// A knowledge section reader that groups chunks into logical sections based on embedding similarity.
    /// </summary>
    public sealed class SectionReader : IKnowledgeSectionReader
    {
        private readonly SegmentBasedChunkReader _chunkReader;
        private readonly IEmbeddingService _embeddings;
        private readonly SectioningConfiguration _configuration;
        private readonly ITokenizer _tokenizer;

        /// <summary>
        /// Initializes a new instance of the <see cref="SectionReader"/> class.
        /// </summary>
        /// <param name="chunkReader">The chunk reader to read content from.</param>
        /// <param name="embeddings">The embedding service for generating vector representations.</param>
        /// <param name="cfg">The sectioning configuration.</param>
        /// <param name="tokenizer">The tokenizer for counting tokens.</param>
        public SectionReader(
            SegmentBasedChunkReader chunkReader,
            IEmbeddingService embeddings,
            SectioningConfiguration configuration,
            ITokenizer tokenizer)
        {
            _chunkReader = chunkReader ?? throw new ArgumentNullException(nameof(chunkReader));
            _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        }

        /// <summary>
        /// Reads sections from the knowledge file asynchronously, grouping chunks based on embedding similarity.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>An async enumerable of chunk lists, where each list represents a section.</returns>
        public async IAsyncEnumerable<List<KnowledgeFileChunk>> ReadSectionsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Prime the look-ahead queue
            Queue<KnowledgeFileChunk?> lookaheadQueue = new Queue<KnowledgeFileChunk?>();
            for (int i = 0; i < _configuration.LookaheadChunks; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                KnowledgeFileChunk? item = await ReadOneAsync(cancellationToken);
                if (item == null) break;
                lookaheadQueue.Enqueue(item);
            }

            if (lookaheadQueue.Count == 0) yield break;

            // Start the first section
            List<KnowledgeFileChunk> currentSectionChunks = new List<KnowledgeFileChunk>();
            float[]? centroid = null;
            int lowSimilarityStreak = 0;
            int chunkCount = 0;

            while (lookaheadQueue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                KnowledgeFileChunk? candidate = lookaheadQueue.Dequeue();
                if (candidate == null) break;

                // Keep look-ahead filled
                KnowledgeFileChunk? nextItem = await ReadOneAsync(cancellationToken);
                if (nextItem != null)
                {
                    lookaheadQueue.Enqueue(nextItem);
                }

                // Initialize centroid if this is the first chunk
                if (centroid == null)
                {
                    centroid = new float[candidate.Embedding!.Length];
                    Array.Copy(candidate.Embedding, centroid, centroid.Length);
                    currentSectionChunks.Add(candidate);
                    chunkCount++;
                    continue;
                }

                // Compute similarity with current centroid
                float similarity = ConfidenceMath.CosineSimilarity(candidate.Embedding!, centroid);

                // Check if we should split based on similarity
                bool shouldSplit = false;

                if (similarity < _configuration.StopThreshold)
                {
                    lowSimilarityStreak++;
                    if (lowSimilarityStreak >= _configuration.ConfirmWindow)
                    {
                        shouldSplit = true;
                    }
                }
                else if (similarity >= _configuration.StartThreshold)
                {
                    lowSimilarityStreak = 0;
                }

                // Check if section size would be exceeded
                if (SectionSizeExceeded(currentSectionChunks, candidate))
                {
                    shouldSplit = true;
                }

                if (shouldSplit)
                {
                    // Yield current section if it has content
                    if (currentSectionChunks.Count > 0)
                    {
                        yield return new List<KnowledgeFileChunk>(currentSectionChunks);
                    }

                    // Start new section
                    currentSectionChunks.Clear();
                    centroid = new float[candidate.Embedding!.Length];
                    Array.Copy(candidate.Embedding, centroid, centroid.Length);
                    currentSectionChunks.Add(candidate);
                    chunkCount = 1;
                    lowSimilarityStreak = 0;
                }
                else
                {
                    // Add to current section
                    currentSectionChunks.Add(candidate);
                    chunkCount++;
                    ConfidenceMath.UpdateCentroidInPlace(centroid, candidate.Embedding!, chunkCount - 1);
                }
            }

            // Yield final section if it has content
            if (currentSectionChunks.Count > 0)
            {
                yield return new List<KnowledgeFileChunk>(currentSectionChunks);
            }
        }

        private async Task<KnowledgeFileChunk?> ReadOneAsync(CancellationToken cancellationToken)
        {
            string? content = await _chunkReader.ReadNextChunkContentAsync(cancellationToken);
            if (content == null) return null;

            float[] embedding = await _embeddings.EmbedAsync(content, cancellationToken);
            return new KnowledgeFileChunk(Guid.NewGuid(), content, embedding);
        }

        private bool SectionSizeExceeded(List<KnowledgeFileChunk> currentChunks, KnowledgeFileChunk candidateChunk)
        {
            int totalTokens = 0;

            // Count tokens in current chunks
            foreach (KnowledgeFileChunk chunk in currentChunks)
            {
                totalTokens += _tokenizer.CountTokens(chunk.Content);
            }

            // Add candidate chunk tokens
            totalTokens += _tokenizer.CountTokens(candidateChunk.Content);

            return totalTokens > _configuration.MaxTokensPerSection;
        }
    }
}