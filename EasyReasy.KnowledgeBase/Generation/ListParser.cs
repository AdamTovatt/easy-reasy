namespace EasyReasy.KnowledgeBase.Generation
{
    /// <summary>
    /// Static helper class for parsing lists from text content.
    /// Handles various list formats including numbered lists, bullet points, and plain text lists.
    /// </summary>
    public static class ListParser
    {
        /// <summary>
        /// Parses a text string containing a list and extracts the individual items as a list of strings.
        /// </summary>
        /// <param name="text">The text containing the list to parse. Can include numbered lists (1., 2., etc.), 
        /// bullet points (-, *, •), or plain text items separated by newlines.</param>
        /// <returns>A list of strings containing the parsed items, with any numbering or bullet points removed.</returns>
        /// <remarks>
        /// This method handles various list formats:
        /// - Numbered lists: "1. Item one\n2. Item two"
        /// - Bullet points: "- Item one\n- Item two" or "* Item one\n* Item two"
        /// - Plain text: Items separated by newlines without formatting
        /// 
        /// The method will:
        /// - Split the text by newlines
        /// - Remove common list markers (numbers, bullets, etc.)
        /// - Trim whitespace from each item
        /// - Filter out empty or whitespace-only items
        /// - Return a clean list of strings
        /// </remarks>
        public static List<string>? ParseList(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            List<string> nonEmptyLines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
            
            if (nonEmptyLines.Count == 0)
                return null;

            List<string> result = new List<string>();
            bool foundValidListMarkers = false;

            foreach (string originalLine in nonEmptyLines)
            {
                string line = originalLine.Trim();
                string? extractedItem = null;
                bool isListItem = false;

                // Check for numbered list (1., 2., etc.) - must have content after the marker
                if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d+\.\s+\S"))
                {
                    extractedItem = System.Text.RegularExpressions.Regex.Replace(line, @"^\d+\.\s+", "").Trim();
                    isListItem = true;
                    foundValidListMarkers = true;
                }
                // Check for bullet points (-, *) - must have content after the marker
                else if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^[-*]\s+\S"))
                {
                    extractedItem = System.Text.RegularExpressions.Regex.Replace(line, @"^[-*]\s+", "").Trim();
                    isListItem = true;
                    foundValidListMarkers = true;
                }
                // Check for malformed numbers (digit followed by space, but no dot) - must have content
                else if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d+\s+\S"))
                {
                    extractedItem = System.Text.RegularExpressions.Regex.Replace(line, @"^\d+\s+", "").Trim();
                    isListItem = true;
                    foundValidListMarkers = true;
                }

                if (isListItem && !string.IsNullOrWhiteSpace(extractedItem))
                {
                    result.Add(extractedItem);
                }
            }

            // If no valid list markers were found, try treating as plain text list
            if (!foundValidListMarkers)
            {
                // Check if all lines look like empty list markers
                bool allLooksLikeEmptyMarkers = nonEmptyLines.All(line =>
                {
                    string trimmed = line.Trim();
                    return System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\.$") ||  // "1."
                           System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[-*]$");     // "-" or "*"
                });
                
                if (allLooksLikeEmptyMarkers)
                {
                    return null;
                }
                
                // Single line without markers - not a valid list
                if (nonEmptyLines.Count == 1)
                {
                    return null;
                }
                
                // Multiple lines without markers - treat as plain text list
                result.AddRange(nonEmptyLines.Select(line => line.Trim()));
            }

            // If we found markers but no content, or no valid items, return null
            if (result.Count == 0)
                return null;

            return result;
        }
    }
} 