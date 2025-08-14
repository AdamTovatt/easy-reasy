using EasyReasy.KnowledgeBase.Generation;

namespace EasyReasy.KnowledgeBase.Tests.Generation
{
    [TestClass]
    public class ListParserTests
    {
        [TestMethod]
        public void ParseList_WithNumberedList_ShouldExtractItems()
        {
            // Arrange
            string input = "1. First item\n2. Second item\n3. Third item";

            // Act
            List<string>? result = ListParser.ParseList(input);

            // Assert
            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(new List<string> { "First item", "Second item", "Third item" }, result);
        }

        [TestMethod]
        public void ParseList_WithBulletPoints_ShouldExtractItems()
        {
            // Arrange
            string input = "- First item\n- Second item\n- Third item";

            // Act
            List<string>? result = ListParser.ParseList(input);

            // Assert
            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(new List<string> { "First item", "Second item", "Third item" }, result);
        }

        [TestMethod]
        public void ParseList_WithAsteriskBullets_ShouldExtractItems()
        {
            // Arrange
            string input = "* First item\n* Second item\n* Third item";

            // Act
            List<string>? result = ListParser.ParseList(input);

            // Assert
            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(new List<string> { "First item", "Second item", "Third item" }, result);
        }

        [TestMethod]
        public void ParseList_WithPlainTextItems_ShouldExtractItems()
        {
            // Arrange
            string input = "First item\nSecond item\nThird item";

            // Act
            List<string>? result = ListParser.ParseList(input);

            // Assert
            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(new List<string> { "First item", "Second item", "Third item" }, result);
        }

        [TestMethod]
        public void ParseList_WithLLMResponse_ShouldExtractItems()
        {
            // Arrange
            string input = "Sure, I can give you a list:\n\n1. First item\n2. Second item\n3. Third item\n\nI hope this helps!";

            // Act
            List<string>? result = ListParser.ParseList(input);

            // Assert
            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(new List<string> { "First item", "Second item", "Third item" }, result);
        }

        [TestMethod]
        public void ParseList_WithExtraWhitespace_ShouldExtractItems()
        {
            // Arrange
            string input = "  1.  First item  \n  2.  Second item  \n  3.  Third item  ";

            // Act
            List<string>? result = ListParser.ParseList(input);

            // Assert
            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(new List<string> { "First item", "Second item", "Third item" }, result);
        }

        [TestMethod]
        public void ParseList_WithEmptyLines_ShouldFilterThem()
        {
            // Arrange
            string input = "1. First item\n\n2. Second item\n\n3. Third item";

            // Act
            List<string>? result = ListParser.ParseList(input);

            // Assert
            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(new List<string> { "First item", "Second item", "Third item" }, result);
        }

        [TestMethod]
        public void ParseList_WithMixedFormats_ShouldExtractItems()
        {
            // Arrange
            string input = "Here are some items:\n1. Numbered item\n- Bullet item\n* Asterisk item\n2. Another numbered item";

            // Act
            List<string>? result = ListParser.ParseList(input);

            // Assert
            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(new List<string> { "Numbered item", "Bullet item", "Asterisk item", "Another numbered item" }, result);
        }

        [TestMethod]
        public void ParseList_WithSingleItem_ShouldExtractItem()
        {
            // Arrange
            string input = "1. Single item";

            // Act
            List<string>? result = ListParser.ParseList(input);

            // Assert
            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(new List<string> { "Single item" }, result);
        }

        [TestMethod]
        public void ParseList_WithEmptyInput_ShouldReturnEmptyList()
        {
            // Arrange
            string input = "";

            // Act
            List<string>? result = ListParser.ParseList(input);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ParseList_WithNullInput_ShouldReturnEmptyList()
        {
            // Arrange
            string input = null!;

            // Act
            List<string>? result = ListParser.ParseList(input);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ParseList_WithWhitespaceOnly_ShouldReturnEmptyList()
        {
            // Arrange
            string input = "   \n\n   \t   \n   ";

            // Act
            List<string>? result = ListParser.ParseList(input);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ParseList_WithNoListContent_ShouldReturnEmptyList()
        {
            // Arrange
            string input = "This is just a paragraph of text without any list items. It has no numbered items or bullet points.";

            // Act
            List<string>? result = ListParser.ParseList(input);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ParseList_WithOnlyListMarkers_ShouldReturnEmptyList()
        {
            // Arrange
            string input = "1.\n2.\n-\n*\n3.";

            // Act
            List<string>? result = ListParser.ParseList(input);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ParseList_WithMalformedNumbers_ShouldRemoveNumbers()
        {
            // Arrange
            string input = "1 First item\n2 Second item\n3 Third item";

            // Act
            List<string>? result = ListParser.ParseList(input);

            // Assert
            CollectionAssert.AreEqual(new List<string> { "First item", "Second item", "Third item" }, result);
        }
    }
}
