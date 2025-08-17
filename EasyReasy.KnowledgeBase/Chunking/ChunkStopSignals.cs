namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// Provides predefined chunk stop signal arrays for different text formats.
    /// </summary>
    public class ChunkStopSignals
    {
        /// <summary>
        /// Gets an array of chunk stop signals optimized for Markdown content.
        /// These signals will cause chunking to stop, ensuring they appear at the start of the next chunk.
        /// </summary>
        /// <remarks>
        /// The stop signals include:
        /// - Heading markers (#, ##, ###, etc.)
        /// - Code block markers (```)
        /// - Bold text markers (**)
        /// </remarks>
        public static readonly string[] Markdown =
        [
                "# ",       // Top-level heading
            "## ",      // Second-level heading
            "### ",     // Third-level heading
            "#### ",    // Fourth-level heading
            "##### ",   // Fifth-level heading
            "###### ",  // Sixth-level heading
            "```",      // Code block
            "**",       // Bold text
        ];
    }
}