namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// Configuration for sectioning knowledge files into logical groups based on content similarity.
    /// </summary>
    public sealed class SectioningConfiguration
    {
        /// <summary>
        /// Gets the maximum number of tokens allowed per section.
        /// </summary>
        public int MaxTokensPerSection { get; }

        /// <summary>
        /// Gets the similarity threshold above which chunks are considered similar enough to start a new section.
        /// </summary>
        public double StartThreshold { get; }

        /// <summary>
        /// Gets the similarity threshold below which chunks are considered different enough to potentially end a section.
        /// </summary>
        public double StopThreshold { get; }

        /// <summary>
        /// Gets the number of chunks to look ahead when making sectioning decisions.
        /// </summary>
        public int LookaheadChunks { get; }

        /// <summary>
        /// Gets the number of consecutive low-similarity chunks required to confirm a section split.
        /// </summary>
        public int ConfirmWindow { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SectioningConfiguration"/> class.
        /// </summary>
        /// <param name="maxTokensPerSection">The maximum number of tokens allowed per section. Default is 4000.</param>
        /// <param name="startThreshold">The similarity threshold above which chunks are considered similar enough to start a new section. Default is 0.78.</param>
        /// <param name="stopThreshold">The similarity threshold below which chunks are considered different enough to potentially end a section. Default is 0.72.</param>
        /// <param name="lookaheadChunks">The number of chunks to look ahead when making sectioning decisions. Default is 1.</param>
        /// <param name="confirmWindow">The number of consecutive low-similarity chunks required to confirm a section split. Default is 2.</param>
        public SectioningConfiguration(
            int maxTokensPerSection = 4000,
            double startThreshold = 0.78,
            double stopThreshold = 0.72,
            int lookaheadChunks = 1,
            int confirmWindow = 2)
        {
            MaxTokensPerSection = maxTokensPerSection;
            StartThreshold = startThreshold;
            StopThreshold = stopThreshold;
            LookaheadChunks = Math.Max(0, Math.Min(lookaheadChunks, 3));
            ConfirmWindow = Math.Max(1, confirmWindow);
        }
    }
}