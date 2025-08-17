namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// Provides predefined break string arrays for different text formats.
    /// </summary>
    public class TextSegmentSplitters
    {
        /// <summary>
        /// Gets an array of break strings optimized for Markdown content segmentation.
        /// The strings are ordered by preference, with more specific patterns appearing first.
        /// </summary>
        /// <remarks>
        /// The break strings include:
        /// - Heading markers (#, ##, ###, etc.)
        /// - Paragraph breaks (double line breaks)
        /// - List item markers (-, *, +, 1.)
        /// - Code block endings (```)
        /// - Line breaks
        /// - Sentence endings (. ! ?)
        /// </remarks>
        public static readonly string[] Markdown =
        [
                "\n\n# ",    // New top-level heading
            "\n## ",     // New second-level heading
            "\n### ",    // New third-level heading
            "\n#### ",   // New fourth-level heading
            "\n##### ",  // New fifth-level heading
            "\n###### ", // New sixth-level heading
            "\n\n",      // Double line breaks (paragraph breaks)
            "\n- ",      // Unordered list items
            "\n* ",      // Alternative unordered list items
            "\n+ ",      // Alternative unordered list items
            "\n1. ",     // Ordered list items (simplified - just checking for "1.")
            "\n```\n",   // End of code blocks
            "\n",        // Single line breaks
            ". ",        // Sentence endings
            "! ",        // Exclamation marks
            "? "         // Question marks
        ];
    }
}
