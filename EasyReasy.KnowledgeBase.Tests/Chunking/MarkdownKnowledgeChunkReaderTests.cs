using EasyReasy.KnowledgeBase.Chunking;

namespace EasyReasy.KnowledgeBase.Tests.Chunking
{
    [TestClass]
    public class MarkdownKnowledgeChunkReaderTests
    {
        private static ResourceManager _resourceManager = null!;

        [ClassInitialize]
        public static async Task BeforeAll(TestContext testContext)
        {
            _resourceManager = await ResourceManager.CreateInstanceAsync();
        }

        [TestMethod]
        public async Task ReadNextChunkContentAsync_TestDocument01_ResultsInCorrectChunks()
        {
            using (Stream contentStream = await _resourceManager.GetResourceStreamAsync(TestDataFiles.TestDocument01))
            using (StreamReader reader = new StreamReader(contentStream))
            {
                MarkdownKnowledgeChunkReader chunkReader = new MarkdownKnowledgeChunkReader(reader, 100);

                List<string> chunks = new List<string>();

                while (true)
                {
                    string? chunkContent = await chunkReader.ReadNextChunkContentAsync();

                    if (chunkContent == null)
                        break;

                    chunks.Add(chunkContent);
                }

                foreach (string chunk in chunks)
                {
                    Console.WriteLine("--- Chunk Start ----");
                    Console.WriteLine(chunk);
                    Console.WriteLine("--- Chunk End ----");
                }
            }
        }
    }
}
