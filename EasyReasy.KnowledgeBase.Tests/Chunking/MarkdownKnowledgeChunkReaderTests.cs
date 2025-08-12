using EasyReasy.KnowledgeBase.BertTokenization;
using EasyReasy.KnowledgeBase.Chunking;

namespace EasyReasy.KnowledgeBase.Tests.Chunking
{
    [TestClass]
    public class MarkdownKnowledgeChunkReaderTests
    {
        private static ResourceManager _resourceManager = null!;
        private static ITokenizer _tokenizer = null!;

        [ClassInitialize]
        public static async Task BeforeAll(TestContext testContext)
        {
            _resourceManager = await ResourceManager.CreateInstanceAsync();
            _tokenizer = await BertTokenizer.CreateAsync();
        }
    }
}