namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// Configuration for sectioning knowledge files into logical groups based on content similarity.
    /// Uses statistical analysis of similarity distributions to automatically determine section boundaries.
    /// </summary>
    public sealed class SectioningConfiguration
    {
        /// <summary>
        /// Gets the maximum number of tokens allowed per section.
        /// </summary>
        public int MaxTokensPerSection { get; }

        /// <summary>
        /// Gets the size of the lookahead buffer used for statistical analysis of similarity patterns.
        /// Larger buffers provide more robust statistics but use more memory.
        /// </summary>
        public int LookaheadBufferSize { get; }

        /// <summary>
        /// Gets the standard deviation multiplier used to determine section split thresholds.
        /// The split threshold is calculated as: max(MinimumSimilarityThreshold, mean_similarity - (StandardDeviationMultiplier * std_deviation)).
        /// Typical values are 0.8-1.5, where higher values create fewer, larger sections.
        /// </summary>
        public double StandardDeviationMultiplier { get; }

        /// <summary>
        /// Gets the absolute minimum similarity threshold below which chunks are always considered for section splits.
        /// This prevents overly permissive thresholds when statistical analysis produces very low values.
        /// Typical values are 0.6-0.75 for cosine similarity.
        /// </summary>
        public double MinimumSimilarityThreshold { get; }

        /// <summary>
        /// Gets the token usage percentage at which the sectioning becomes more strict to encourage splits.
        /// When a section reaches this percentage of MaxTokensPerSection, the effective split threshold increases.
        /// Value should be between 0.5 and 0.95. Default is 0.75 (75%).
        /// </summary>
        public double TokenStrictnessThreshold { get; }

        /// <summary>
        /// Gets the minimum number of chunks required before a section can be split.
        /// This prevents tiny sections created by chunks with minimal semantic content (e.g., just markdown headers).
        /// Default is 2 chunks.
        /// </summary>
        public int MinimumChunksPerSection { get; }

        /// <summary>
        /// Gets the minimum number of tokens required before a section can be split.
        /// This works in conjunction with MinimumChunksPerSection to ensure sections have meaningful content.
        /// Default is 50 tokens.
        /// </summary>
        public int MinimumTokensPerSection { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SectioningConfiguration"/> class.
        /// </summary>
        /// <param name="maxTokensPerSection">The maximum number of tokens allowed per section. Default is 4000.</param>
        /// <param name="lookaheadBufferSize">The size of the lookahead buffer for statistical analysis. Default is 100 chunks. Maximum is 500 to prevent excessive memory usage.</param>
        /// <param name="standardDeviationMultiplier">The standard deviation multiplier for determining split thresholds. Default is 1.0, which creates more aggressive splitting than the previous 1.5 default.</param>
        /// <param name="minimumSimilarityThreshold">The absolute minimum similarity threshold. Default is 0.65, preventing overly permissive thresholds.</param>
        /// <param name="tokenStrictnessThreshold">The token usage percentage at which sectioning becomes stricter. Default is 0.75 (75%).</param>
        /// <param name="minimumChunksPerSection">The minimum number of chunks required before a section can be split. Default is 2.</param>
        /// <param name="minimumTokensPerSection">The minimum number of tokens required before a section can be split. Default is 50.</param>
        public SectioningConfiguration(
            int maxTokensPerSection = 4000,
            int lookaheadBufferSize = 100,
            double standardDeviationMultiplier = 1.0,
            double minimumSimilarityThreshold = 0.65,
            double tokenStrictnessThreshold = 0.75,
            int minimumChunksPerSection = 2,
            int minimumTokensPerSection = 50)
        {
            MaxTokensPerSection = maxTokensPerSection;
            LookaheadBufferSize = Math.Max(10, Math.Min(lookaheadBufferSize, 500));
            StandardDeviationMultiplier = Math.Max(0.5, Math.Min(standardDeviationMultiplier, 3.0));
            MinimumSimilarityThreshold = Math.Max(0.4, Math.Min(minimumSimilarityThreshold, 0.9));
            TokenStrictnessThreshold = Math.Max(0.5, Math.Min(tokenStrictnessThreshold, 0.95));
            MinimumChunksPerSection = Math.Max(1, Math.Min(minimumChunksPerSection, 10));
            MinimumTokensPerSection = Math.Max(10, Math.Min(minimumTokensPerSection, 500));
        }
    }
}