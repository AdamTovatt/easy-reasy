using EasyReasy.KnowledgeBase.ConfidenceRating;
using EasyReasy.KnowledgeBase.Generation;
using EasyReasy.KnowledgeBase.Models;
using System.Runtime.CompilerServices;

namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// Represents temporary chunk content during processing before section assignment.
    /// </summary>
    internal readonly record struct ChunkContent(string Content, float[] Embedding);

    /// <summary>
    /// A knowledge section reader that groups chunks into logical sections based on embedding similarity.
    /// </summary>
    public sealed class SectionReader : IKnowledgeSectionReader, IDisposable
    {
        private readonly SegmentBasedChunkReader _chunkReader;
        private readonly IEmbeddingService _embeddings;
        private readonly SectioningConfiguration _configuration;
        private readonly ITokenizer _tokenizer;
        private readonly Guid _fileId;
        private readonly StreamReader? _ownedStreamReader;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SectionReader"/> class.
        /// </summary>
        /// <param name="chunkReader">The chunk reader to read content from.</param>
        /// <param name="embeddings">The embedding service for generating vector representations.</param>
        /// <param name="configuration">The sectioning configuration.</param>
        /// <param name="tokenizer">The tokenizer for counting tokens.</param>
        /// <param name="fileId">The unique identifier of the knowledge file being processed.</param>
        public SectionReader(
            SegmentBasedChunkReader chunkReader,
            IEmbeddingService embeddings,
            SectioningConfiguration configuration,
            ITokenizer tokenizer,
            Guid fileId)
            : this(chunkReader, embeddings, configuration, tokenizer, fileId, ownedStreamReader: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SectionReader"/> class with an optionally owned StreamReader.
        /// </summary>
        /// <param name="chunkReader">The chunk reader to read content from.</param>
        /// <param name="embeddings">The embedding service for generating vector representations.</param>
        /// <param name="configuration">The sectioning configuration.</param>
        /// <param name="tokenizer">The tokenizer for counting tokens.</param>
        /// <param name="fileId">The unique identifier of the knowledge file being processed.</param>
        /// <param name="ownedStreamReader">The StreamReader that this instance owns and should dispose, or null if not owned.</param>
        internal SectionReader(
            SegmentBasedChunkReader chunkReader,
            IEmbeddingService embeddings,
            SectioningConfiguration configuration,
            ITokenizer tokenizer,
            Guid fileId,
            StreamReader? ownedStreamReader)
        {
            _chunkReader = chunkReader ?? throw new ArgumentNullException(nameof(chunkReader));
            _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _fileId = fileId;
            _ownedStreamReader = ownedStreamReader;
        }

        /// <summary>
        /// Reads sections from the knowledge file asynchronously, grouping chunks based on statistical analysis of embedding similarity.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>An async enumerable of chunk lists, where each list represents a section.</returns>
        public async IAsyncEnumerable<List<KnowledgeFileChunk>> ReadSectionsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Prime the look-ahead buffer
            Queue<ChunkContent?> lookaheadBuffer = new Queue<ChunkContent?>();
            for (int i = 0; i < _configuration.LookaheadBufferSize; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ChunkContent? item = await ReadOneAsync(cancellationToken);
                if (item == null) break;
                lookaheadBuffer.Enqueue(item);
            }

            if (lookaheadBuffer.Count == 0) yield break;

            // Start the first section
            List<ChunkContent> currentSectionChunks = new List<ChunkContent>();
            float[]? centroid = null;
            int chunkCount = 0;
            int currentSectionIndex = 0;

            while (lookaheadBuffer.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ChunkContent? candidate = lookaheadBuffer.Dequeue();
                if (candidate == null) break;

                // Keep look-ahead buffer filled
                ChunkContent? nextItem = await ReadOneAsync(cancellationToken);
                if (nextItem != null)
                {
                    lookaheadBuffer.Enqueue(nextItem);
                }

                // Initialize centroid if this is the first chunk
                if (centroid == null)
                {
                    centroid = new float[candidate.Value.Embedding.Length];
                    Array.Copy(candidate.Value.Embedding, centroid, centroid.Length);
                    currentSectionChunks.Add(candidate.Value);
                    chunkCount++;
                    continue;
                }

                // Compute similarity with current centroid
                float similarity = ConfidenceMath.CosineSimilarity(candidate.Value.Embedding, centroid);

                // Check if we should split based on statistical analysis
                bool shouldSplit = false;

                // Calculate statistical split threshold using lookahead buffer
                double splitThreshold = CalculateStatisticalSplitThreshold(centroid, lookaheadBuffer, currentSectionChunks);

                if (similarity < splitThreshold)
                {
                    // Check minimum section constraints before allowing split
                    if (SectionMeetsMinimumRequirements(currentSectionChunks, candidate.Value))
                    {
                        shouldSplit = true;
                    }
                }

                // Check if section size would be exceeded
                if (SectionSizeExceeded(currentSectionChunks, candidate.Value))
                {
                    shouldSplit = true;
                }

                if (shouldSplit)
                {
                    // Yield current section if it has content
                    if (currentSectionChunks.Count > 0)
                    {
                        yield return ConvertToKnowledgeFileChunks(currentSectionChunks, currentSectionIndex);
                        currentSectionIndex++;
                    }

                    // Start new section
                    currentSectionChunks.Clear();
                    centroid = new float[candidate.Value.Embedding.Length];
                    Array.Copy(candidate.Value.Embedding, centroid, centroid.Length);
                    currentSectionChunks.Add(candidate.Value);
                    chunkCount = 1;
                }
                else
                {
                    // Add to current section
                    currentSectionChunks.Add(candidate.Value);
                    chunkCount++;
                    ConfidenceMath.UpdateCentroidInPlace(centroid, candidate.Value.Embedding, chunkCount - 1);
                }
            }

            // Yield final section if it has content
            if (currentSectionChunks.Count > 0)
            {
                yield return ConvertToKnowledgeFileChunks(currentSectionChunks, currentSectionIndex);
            }
        }

        /// <summary>
        /// Converts a list of ChunkContent to a list of KnowledgeFileChunk objects with proper section relationships.
        /// </summary>
        /// <param name="chunkContents">The chunk content objects to convert.</param>
        /// <param name="sectionIndex">The index of the section these chunks belong to.</param>
        /// <returns>A list of KnowledgeFileChunk objects with proper relationships.</returns>
        private List<KnowledgeFileChunk> ConvertToKnowledgeFileChunks(List<ChunkContent> chunkContents, int sectionIndex)
        {
            Guid sectionId = Guid.NewGuid();
            List<KnowledgeFileChunk> chunks = new List<KnowledgeFileChunk>();

            for (int i = 0; i < chunkContents.Count; i++)
            {
                ChunkContent chunkContent = chunkContents[i];
                KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                    id: Guid.NewGuid(),
                    sectionId: sectionId,
                    chunkIndex: i,
                    content: chunkContent.Content,
                    embedding: chunkContent.Embedding);
                chunks.Add(chunk);
            }

            return chunks;
        }

        private async Task<ChunkContent?> ReadOneAsync(CancellationToken cancellationToken)
        {
            string? content = await _chunkReader.ReadNextChunkContentAsync(cancellationToken);
            if (content == null) return null;

            float[] embedding = await _embeddings.EmbedAsync(content, cancellationToken);
            return new ChunkContent(content, embedding);
        }

        /// <summary>
        /// Calculates the statistical split threshold based on similarity distribution in the lookahead buffer.
        /// Incorporates token-based strictness to encourage splits as sections approach their maximum size.
        /// </summary>
        /// <param name="centroid">Current section centroid.</param>
        /// <param name="lookaheadBuffer">Buffer of upcoming chunks for statistical analysis.</param>
        /// <param name="currentSectionChunks">Chunks in the current section for fallback statistics.</param>
        /// <returns>The similarity threshold below which a split should occur.</returns>
        private double CalculateStatisticalSplitThreshold(
            float[] centroid,
            Queue<ChunkContent?> lookaheadBuffer,
            List<ChunkContent> currentSectionChunks)
        {
            List<double> similarities = new List<double>();

            // Calculate similarities for lookahead chunks
            foreach (ChunkContent? chunk in lookaheadBuffer)
            {
                if (chunk != null)
                {
                    double similarity = ConfidenceMath.CosineSimilarity(chunk.Value.Embedding, centroid);
                    similarities.Add(similarity);
                }
            }

            // If we don't have enough lookahead data, use current section's internal similarities as fallback
            if (similarities.Count < 5 && currentSectionChunks.Count > 1)
            {
                foreach (ChunkContent chunk in currentSectionChunks)
                {
                    double similarity = ConfidenceMath.CosineSimilarity(chunk.Embedding, centroid);
                    similarities.Add(similarity);
                }
            }

            // Need at least 3 data points for meaningful statistics
            if (similarities.Count < 3)
            {
                // Fallback: use minimum threshold with some token-based adjustment
                return CalculateTokenAdjustedThreshold(_configuration.MinimumSimilarityThreshold, currentSectionChunks);
            }

            // Calculate mean and standard deviation
            double mean = similarities.Average();
            double variance = similarities.Select(x => Math.Pow(x - mean, 2)).Average();
            double standardDeviation = Math.Sqrt(variance);

            // Calculate base statistical threshold
            double statisticalThreshold = mean - (_configuration.StandardDeviationMultiplier * standardDeviation);

            // Apply minimum threshold constraint
            double baseThreshold = Math.Max(_configuration.MinimumSimilarityThreshold, statisticalThreshold);

            // Apply token-based strictness adjustment
            double finalThreshold = CalculateTokenAdjustedThreshold(baseThreshold, currentSectionChunks);

            // Ensure threshold is reasonable (between minimum and 0.95 for cosine similarity)
            return Math.Max(_configuration.MinimumSimilarityThreshold, Math.Min(finalThreshold, 0.95));
        }

        /// <summary>
        /// Adjusts the split threshold based on current token usage to encourage splits as sections grow large.
        /// </summary>
        /// <param name="baseThreshold">The base similarity threshold before token adjustment.</param>
        /// <param name="currentSectionChunks">Chunks in the current section.</param>
        /// <returns>The adjusted threshold that becomes more strict as token usage increases.</returns>
        private double CalculateTokenAdjustedThreshold(double baseThreshold, List<ChunkContent> currentSectionChunks)
        {
            // Calculate current token usage
            int currentTokens = currentSectionChunks.Sum(chunk => _tokenizer.CountTokens(chunk.Content));
            double tokenUsageRatio = (double)currentTokens / _configuration.MaxTokensPerSection;

            // If we haven't reached the strictness threshold, return base threshold
            if (tokenUsageRatio < _configuration.TokenStrictnessThreshold)
            {
                return baseThreshold;
            }

            // Calculate strictness multiplier (increases as we approach max tokens)
            double excessRatio = (tokenUsageRatio - _configuration.TokenStrictnessThreshold) /
                                (1.0 - _configuration.TokenStrictnessThreshold);

            // Apply exponential increase in strictness (quadratic growth)
            double strictnessMultiplier = 1.0 + (excessRatio * excessRatio * 0.5); // Max 50% increase

            // Increase the threshold to make splits more likely
            return baseThreshold * strictnessMultiplier;
        }

        /// <summary>
        /// Checks if the current section meets the minimum requirements for splitting.
        /// Considers minimum chunk count, token count, and chunk stop signal awareness.
        /// </summary>
        /// <param name="currentSectionChunks">Chunks in the current section.</param>
        /// <param name="candidateChunk">The chunk being considered for the section.</param>
        /// <returns>True if the section can be split, false if it should continue growing.</returns>
        private bool SectionMeetsMinimumRequirements(List<ChunkContent> currentSectionChunks, ChunkContent candidateChunk)
        {
            // Check minimum chunk count
            if (currentSectionChunks.Count < _configuration.MinimumChunksPerSection)
            {
                return false;
            }

            // Check minimum token count
            int currentTokens = currentSectionChunks.Sum(chunk => _tokenizer.CountTokens(chunk.Content));
            if (currentTokens < _configuration.MinimumTokensPerSection)
            {
                return false;
            }

            // If we have chunk stop signals configured, be more lenient with small sections
            // that start with stop signals (they need more content to be meaningful)
            if (_configuration.ChunkStopSignals.Length > 0 && currentSectionChunks.Count <= 2)
            {
                // Check if the candidate chunk (which would start the next section) begins with a stop signal
                if (StartsWithStopSignal(candidateChunk.Content))
                {
                    // This chunk starts with a stop signal, but if the current section is very small,
                    // require it to be larger before splitting
                    return currentTokens >= _configuration.MinimumTokensPerSection * 1.5; // 50% more tokens required
                }

                // Check if the current section's last chunk starts with a stop signal
                if (currentSectionChunks.Count > 0 && StartsWithStopSignal(currentSectionChunks.Last().Content))
                {
                    // The section ends with a stop signal chunk - allow split if we meet basic minimums
                    return true;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if the given content starts with any of the configured chunk stop signals.
        /// </summary>
        /// <param name="content">The content to check.</param>
        /// <returns>True if the content starts with a stop signal, false otherwise.</returns>
        private bool StartsWithStopSignal(string content)
        {
            if (string.IsNullOrEmpty(content) || _configuration.ChunkStopSignals.Length == 0)
                return false;

            foreach (string stopSignal in _configuration.ChunkStopSignals)
            {
                if (!string.IsNullOrEmpty(stopSignal) && content.StartsWith(stopSignal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool SectionSizeExceeded(List<ChunkContent> currentChunks, ChunkContent candidateChunk)
        {
            int totalTokens = 0;

            // Count tokens in current chunks
            foreach (ChunkContent chunk in currentChunks)
            {
                totalTokens += _tokenizer.CountTokens(chunk.Content);
            }

            // Add candidate chunk tokens
            totalTokens += _tokenizer.CountTokens(candidateChunk.Content);

            return totalTokens > _configuration.MaxTokensPerSection;
        }

        /// <summary>
        /// Disposes the resources used by the SectionReader.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the SectionReader and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _ownedStreamReader?.Dispose();
            }

            _disposed = true;
        }
    }
}