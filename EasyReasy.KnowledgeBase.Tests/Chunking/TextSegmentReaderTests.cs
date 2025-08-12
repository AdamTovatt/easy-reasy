using EasyReasy.KnowledgeBase.Chunking;

namespace EasyReasy.KnowledgeBase.Tests.Chunking
{
    [TestClass]
    public class TextSegmentReaderTests
    {
        [TestMethod]
        public async Task ReadNextTextSegmentAsync_ShouldReturnNull_WhenNoContent()
        {
            // Arrange
            string content = "";
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            TextSegmentReader segmentReader = new TextSegmentReader(reader, "\n", ". ");

            // Act
            string? result = await segmentReader.ReadNextTextSegmentAsync();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task ReadNextTextSegmentAsync_ShouldSplitAtLineBreaks()
        {
            // Arrange
            string content = "First line.\nSecond line.\nThird line.";
            Console.WriteLine($"Original content:\n{content}");
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            TextSegmentReader segmentReader = new TextSegmentReader(reader, "\n", ". ");

            // Act
            string? firstSegment = await segmentReader.ReadNextTextSegmentAsync();
            string? secondSegment = await segmentReader.ReadNextTextSegmentAsync();
            string? thirdSegment = await segmentReader.ReadNextTextSegmentAsync();

            Console.WriteLine($"First segment:\n{firstSegment}");
            Console.WriteLine($"Second segment:\n{secondSegment}");
            Console.WriteLine($"Third segment:\n{thirdSegment}");

            // Assert
            Assert.IsNotNull(firstSegment, "First segment should not be null");
            Assert.IsNotNull(secondSegment, "Second segment should not be null");
            Assert.IsNotNull(thirdSegment, "Third segment should not be null");
            Assert.IsTrue(firstSegment.Contains("First line."), $"First segment should contain 'First line.'. Segment content: {firstSegment}");
            Assert.IsTrue(secondSegment.Contains("Second line."), $"Second segment should contain 'Second line.'. Segment content: {secondSegment}");
            Assert.IsTrue(thirdSegment.Contains("Third line."), $"Third segment should contain 'Third line.'. Segment content: {thirdSegment}");
        }

        [TestMethod]
        public async Task ReadNextTextSegmentAsync_ShouldSplitAtPeriods_WhenNoLineBreaks()
        {
            // Arrange
            string content = "This is sentence one. This is sentence two. This is sentence three.";
            Console.WriteLine($"Original content:\n{content}");
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            TextSegmentReader segmentReader = new TextSegmentReader(reader, "\n", ". ");

            // Act
            string? firstSegment = await segmentReader.ReadNextTextSegmentAsync();
            string? secondSegment = await segmentReader.ReadNextTextSegmentAsync();
            string? thirdSegment = await segmentReader.ReadNextTextSegmentAsync();

            Console.WriteLine($"First segment:\n\"{firstSegment}\"");
            Console.WriteLine($"Second segment:\n\"{secondSegment}\"");
            Console.WriteLine($"Third segment:\n\"{thirdSegment}");

            // Assert
            Assert.IsNotNull(firstSegment, "First segment should not be null");
            Assert.IsNotNull(secondSegment, "Second segment should not be null");
            Assert.IsNotNull(thirdSegment, "Third segment should not be null");
            Assert.IsTrue(firstSegment.Contains("This is sentence one."), $"First segment should contain 'This is sentence one.'. Segment content: {firstSegment}");
            Assert.IsTrue(secondSegment.Contains("This is sentence two."), $"Second segment should contain 'This is sentence two.'. Segment content: {secondSegment}");
            Assert.IsTrue(thirdSegment.Contains("This is sentence three."), $"Third segment should contain 'This is sentence three.'. Segment content: {thirdSegment}");
        }

        [TestMethod]
        public async Task ReadNextTextSegmentAsync_ShouldReturnRemainingContent_WhenNoBreakPointsFound()
        {
            // Arrange
            string content = "Thisisasentencewithoutanybreakpoints";
            Console.WriteLine($"Original content:\n{content}");
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            TextSegmentReader segmentReader = new TextSegmentReader(reader, "\n", ". ");

            // Act
            string? segment = await segmentReader.ReadNextTextSegmentAsync();
            string? secondSegment = await segmentReader.ReadNextTextSegmentAsync();

            Console.WriteLine($"Segment:\n{segment}");

            // Assert
            Assert.IsNotNull(segment, "Segment should not be null");
            Assert.AreEqual(content, segment, "Segment should contain the entire content when no break points are found");
            Assert.IsNull(secondSegment, "Second segment should be null when no more content");
        }

        [TestMethod]
        public async Task ReadNextTextSegmentAsync_ShouldWorkWithCustomBreakStrings()
        {
            // Arrange
            string content = "First part</break>Second part</break>Third part";
            Console.WriteLine($"Original content:\n{content}");
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            TextSegmentReader segmentReader = new TextSegmentReader(reader, "</break>", "\n", ". ");

            // Act
            string? firstSegment = await segmentReader.ReadNextTextSegmentAsync();
            string? secondSegment = await segmentReader.ReadNextTextSegmentAsync();
            string? thirdSegment = await segmentReader.ReadNextTextSegmentAsync();

            Console.WriteLine($"First segment:\n{firstSegment}");
            Console.WriteLine($"Second segment:\n{secondSegment}");
            Console.WriteLine($"Third segment:\n{thirdSegment}");

            // Assert
            Assert.IsNotNull(firstSegment, "First segment should not be null");
            Assert.IsNotNull(secondSegment, "Second segment should not be null");
            Assert.IsNotNull(thirdSegment, "Third segment should not be null");
            Assert.IsTrue(firstSegment.Contains("First part"), $"First segment should contain 'First part'. Segment content: {firstSegment}");
            Assert.IsTrue(firstSegment.EndsWith("</break>"), $"First segment should end with '</break>'. Segment content: {firstSegment}");
            Assert.IsTrue(secondSegment.Contains("Second part"), $"Second segment should contain 'Second part'. Segment content: {secondSegment}");
            Assert.IsTrue(secondSegment.EndsWith("</break>"), $"Second segment should end with '</break>'. Segment content: {secondSegment}");
            Assert.IsTrue(thirdSegment.Contains("Third part"), $"Third segment should contain 'Third part'. Segment content: {thirdSegment}");
        }

        [TestMethod]
        public async Task ReadNextTextSegmentAsync_ShouldHandleMultipleBreakStringsOfSameLength()
        {
            // Arrange
            string content = "First part. Second part! Third part?";
            Console.WriteLine($"Original content:\n{content}");
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            TextSegmentReader segmentReader = new TextSegmentReader(reader, ". ", "! ", "?");

            // Act
            string? firstSegment = await segmentReader.ReadNextTextSegmentAsync();
            string? secondSegment = await segmentReader.ReadNextTextSegmentAsync();
            string? thirdSegment = await segmentReader.ReadNextTextSegmentAsync();

            Console.WriteLine($"First segment:\n{firstSegment}");
            Console.WriteLine($"Second segment:\n{secondSegment}");
            Console.WriteLine($"Third segment:\n{thirdSegment}");

            // Assert
            Assert.IsNotNull(firstSegment, "First segment should not be null");
            Assert.IsNotNull(secondSegment, "Second segment should not be null");
            Assert.IsNotNull(thirdSegment, "Third segment should not be null");
            Assert.IsTrue(firstSegment.EndsWith(". "), $"First segment should end with '. '. Segment content: {firstSegment}");
            Assert.IsTrue(secondSegment.EndsWith("! "), $"Second segment should end with '! '. Segment content: {secondSegment}");
            Assert.IsTrue(thirdSegment.EndsWith("?"), $"Third segment should end with '?'. Segment content: {thirdSegment}");
        }

        [TestMethod]
        public async Task ReadNextTextSegmentAsync_RoundTrip_ShouldPreserveOriginalContent()
        {
            // Arrange
            string originalContent = "This is the first paragraph.\n\nThis is the second paragraph. It has multiple sentences. Each sentence ends with a period.\n\nThis is the third paragraph with different punctuation! Some sentences end with exclamation marks? Others end with question marks.\n\nFinal paragraph without trailing newline.";
            Console.WriteLine($"Original content ({originalContent.Length} characters):\n{originalContent}");
            
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(originalContent)));
            TextSegmentReader segmentReader = new TextSegmentReader(reader, "\n\n", "\n", ". ", "! ", "? ");

            // Act - Read all segments
            List<string> segments = new List<string>();
            string? segment;
            while ((segment = await segmentReader.ReadNextTextSegmentAsync()) != null)
            {
                segments.Add(segment);
                Console.WriteLine($"Segment {segments.Count}: \"{segment}\"");
            }

            // Join all segments back together
            string reconstructedContent = string.Concat(segments);
            Console.WriteLine($"Reconstructed content ({reconstructedContent.Length} characters):\n{reconstructedContent}");

            // Assert
            Assert.IsTrue(segments.Count > 0, "Should have read at least one segment");
            Assert.AreEqual(originalContent, reconstructedContent, "Reconstructed content should exactly match the original content");
            Assert.AreEqual(originalContent.Length, reconstructedContent.Length, "Reconstructed content should have the same length as original");
        }
    }
}