namespace EasyReasy.KnowledgeBase.BertTokenization.Tests
{
    [TestClass]
    public sealed class BertTokenizerTests
    {
        private BertTokenizer _tokenizer = null!;

        [TestInitialize]
        public async Task Setup()
        {
            _tokenizer = await BertTokenizer.CreateAsync();
        }

        [TestMethod]
        public void Encode_EmptyString_ReturnsEmptyArray()
        {
            // Act
            int[] result = _tokenizer.Encode(string.Empty);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public void Encode_NullString_ReturnsEmptyArray()
        {
            // Act
            int[] result = _tokenizer.Encode(null!);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public void Encode_SimpleText_ReturnsTokens()
        {
            // Arrange
            string text = "Hello world";

            // Act
            int[] result = _tokenizer.Encode(text);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
        }

        [TestMethod]
        public void Decode_EmptyArray_ReturnsEmptyString()
        {
            // Act
            string result = _tokenizer.Decode(Array.Empty<int>());

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void Decode_NullArray_ReturnsEmptyString()
        {
            // Act
            string result = _tokenizer.Decode(null!);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void EncodeDecode_RoundTrip_ReturnsOriginalText()
        {
            // Arrange
            string originalText = "This is a test sentence.";

            // Act
            int[] tokens = _tokenizer.Encode(originalText);
            string decodedText = _tokenizer.Decode(tokens);

            // Assert
            // BERT tokenizers add [CLS] and [SEP] tokens, so we need to check that the original text is contained
            // within the decoded text, not that they're exactly equal
            Assert.IsTrue(decodedText.Contains(originalText.ToLower()),
                $"Decoded text '{decodedText}' should contain original text '{originalText}'");
        }

        [TestMethod]
        public void CountTokens_EmptyString_ReturnsZero()
        {
            // Act
            int result = _tokenizer.CountTokens(string.Empty);

            // Assert
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void CountTokens_SimpleText_ReturnsCorrectCount()
        {
            // Arrange
            string text = "Hello world";

            // Act
            int result = _tokenizer.CountTokens(text);

            // Assert
            Assert.IsTrue(result > 0);
        }

        [TestMethod]
        public void CountTokens_MatchesEncodeLength()
        {
            // Arrange
            string text = "This is a test sentence with multiple words.";

            // Act
            int countResult = _tokenizer.CountTokens(text);
            int[] encodeResult = _tokenizer.Encode(text);

            // Assert
            Assert.AreEqual(encodeResult.Length, countResult);
        }

        [TestMethod]
        public void Encode_AddsBertSpecialTokens()
        {
            // Arrange
            string text = "Hello world";

            // Act
            int[] tokens = _tokenizer.Encode(text);
            string decodedText = _tokenizer.Decode(tokens);

            // Assert
            // BERT tokenizers should add [CLS] at the beginning and [SEP] at the end
            Assert.IsTrue(decodedText.StartsWith("[CLS]"), "Decoded text should start with [CLS]");
            Assert.IsTrue(decodedText.EndsWith("[SEP]"), "Decoded text should end with [SEP]");
            Assert.IsTrue(decodedText.Contains(text.ToLower()), "Decoded text should contain the original text");
        }

        [TestMethod]
        public void Encode_LongText_HandlesLargeInput()
        {
            // Arrange
            string longText = string.Join(" ", Enumerable.Range(1, 50).Select(i =>
                $"This is sentence number {i} which contains multiple words and should be properly tokenized by the BERT tokenizer."));

            // Act
            int[] tokens = _tokenizer.Encode(longText);
            string decodedText = _tokenizer.Decode(tokens);

            // Assert
            Assert.IsTrue(tokens.Length > 1000, "Long text should produce many tokens");
            Assert.IsTrue(decodedText.StartsWith("[CLS]"), "Decoded text should start with [CLS]");
            Assert.IsTrue(decodedText.EndsWith("[SEP]"), "Decoded text should end with [SEP]");

            // Check that the original text is preserved (case-insensitive due to BERT uncased)
            string originalLower = longText.ToLower();
            string decodedLower = decodedText.ToLower();
            Assert.IsTrue(decodedLower.Contains(originalLower),
                "Decoded text should contain the original long text");
        }
    }
}