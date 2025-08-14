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
        /// <param name="configuration">The sectioning configuration.</param>
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
        /// Reads sections from the knowledge file asynchronously, grouping chunks based on statistical analysis of embedding similarity.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>An async enumerable of chunk lists, where each list represents a section.</returns>
        public async IAsyncEnumerable<List<KnowledgeFileChunk>> ReadSectionsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Prime the look-ahead buffer
            Queue<KnowledgeFileChunk?> lookaheadBuffer = new Queue<KnowledgeFileChunk?>();
            for (int i = 0; i < _configuration.LookaheadBufferSize; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                KnowledgeFileChunk? item = await ReadOneAsync(cancellationToken);
                if (item == null) break;
                lookaheadBuffer.Enqueue(item);
            }

            if (lookaheadBuffer.Count == 0) yield break;
             
            // Start the first section
            List<KnowledgeFileChunk> currentSectionChunks = new List<KnowledgeFileChunk>();
            float[]? centroid = null;
            int chunkCount = 0;

            while (lookaheadBuffer.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                KnowledgeFileChunk? candidate = lookaheadBuffer.Dequeue();
                if (candidate == null) break;

                // Keep look-ahead buffer filled
                KnowledgeFileChunk? nextItem = await ReadOneAsync(cancellationToken);
                if (nextItem != null)
                {
                    lookaheadBuffer.Enqueue(nextItem);
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

                // Check if we should split based on statistical analysis
                bool shouldSplit = false;

                // Calculate statistical split threshold using lookahead buffer
                double splitThreshold = CalculateStatisticalSplitThreshold(centroid, lookaheadBuffer, currentSectionChunks);
                
                if (similarity < splitThreshold)
                {
                    // Check minimum section constraints before allowing split
                    if (SectionMeetsMinimumRequirements(currentSectionChunks, candidate))
                    {
                        shouldSplit = true;
                    }
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
            Queue<KnowledgeFileChunk?> lookaheadBuffer, 
            List<KnowledgeFileChunk> currentSectionChunks)
        {
            List<double> similarities = new List<double>();
            
            // Calculate similarities for lookahead chunks
            foreach (KnowledgeFileChunk? chunk in lookaheadBuffer)
            {
                if (chunk?.Embedding != null)
                {
                    double similarity = ConfidenceMath.CosineSimilarity(chunk.Embedding, centroid);
                    similarities.Add(similarity);
                }
            }

            // If we don't have enough lookahead data, use current section's internal similarities as fallback
            if (similarities.Count < 5 && currentSectionChunks.Count > 1)
            {
                foreach (KnowledgeFileChunk chunk in currentSectionChunks)
                {
                    double similarity = ConfidenceMath.CosineSimilarity(chunk.Embedding!, centroid);
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
        private double CalculateTokenAdjustedThreshold(double baseThreshold, List<KnowledgeFileChunk> currentSectionChunks)
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
        private bool SectionMeetsMinimumRequirements(List<KnowledgeFileChunk> currentSectionChunks, KnowledgeFileChunk candidateChunk)
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