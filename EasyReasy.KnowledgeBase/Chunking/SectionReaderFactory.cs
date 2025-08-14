using EasyReasy.KnowledgeBase.Generation;

namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// Factory for creating SectionReader instances with simplified configuration.
    /// 
    /// <example>
    /// <para><strong>Before (verbose setup):</strong></para>
    /// <code>
    /// using Stream stream = await _resourceManager.GetResourceStreamAsync(TestDataFiles.TestDocument01);
    /// using StreamReader reader = new StreamReader(stream);
    /// ChunkingConfiguration chunkingConfig = new ChunkingConfiguration(_tokenizer, 50);
    /// SectioningConfiguration sectioningConfig = new SectioningConfiguration(maxTokensPerSection: 200, chunkStopSignals: ChunkStopSignals.Markdown);
    /// TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
    /// SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfig);
    /// SectionReader sectionReader = new SectionReader(chunkReader, _ollamaEmbeddingService, sectioningConfig, _tokenizer);
    /// </code>
    /// 
    /// <para><strong>After (simplified with factory):</strong></para>
    /// <code>
    /// SectionReaderFactory factory = new SectionReaderFactory(_ollamaEmbeddingService, _tokenizer);
    /// using Stream stream = await _resourceManager.GetResourceStreamAsync(TestDataFiles.TestDocument01);
    /// SectionReader sectionReader = factory.CreateForMarkdown(stream, maxTokensPerChunk: 50, maxTokensPerSection: 200);
    /// </code>
    /// </example>
    /// </summary>
    public sealed class SectionReaderFactory
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly ITokenizer _tokenizer;

        /// <summary>
        /// Initializes a new instance of the <see cref="SectionReaderFactory"/> class.
        /// </summary>
        /// <param name="embeddingService">The embedding service for generating vector representations.</param>
        /// <param name="tokenizer">The tokenizer for counting tokens.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        public SectionReaderFactory(IEmbeddingService embeddingService, ITokenizer tokenizer)
        {
            _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        }

        /// <summary>
        /// Creates a SectionReader configured for Markdown content using default settings.
        /// </summary>
        /// <param name="stream">The stream to read content from.</param>
        /// <param name="fileId">The unique identifier of the knowledge file being processed.</param>
        /// <returns>A configured SectionReader instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
        public SectionReader CreateForMarkdown(Stream stream, Guid fileId)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            StreamReader streamReader = new StreamReader(stream);
            return CreateForMarkdownInternal(streamReader, fileId, ownedStreamReader: streamReader);
        }

        /// <summary>
        /// Creates a SectionReader configured for Markdown content using default settings.
        /// </summary>
        /// <param name="streamReader">The stream reader to read content from.</param>
        /// <param name="fileId">The unique identifier of the knowledge file being processed.</param>
        /// <returns>A configured SectionReader instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when streamReader is null.</exception>
        public SectionReader CreateForMarkdown(StreamReader streamReader, Guid fileId)
        {
            return CreateForMarkdownInternal(streamReader, fileId, ownedStreamReader: null);
        }

        /// <summary>
        /// Internal method to create a SectionReader with optional StreamReader ownership.
        /// </summary>
        /// <param name="streamReader">The stream reader to read content from.</param>
        /// <param name="fileId">The unique identifier of the knowledge file being processed.</param>
        /// <param name="ownedStreamReader">The StreamReader to be owned and disposed by the SectionReader, or null.</param>
        /// <returns>A configured SectionReader instance.</returns>
        private SectionReader CreateForMarkdownInternal(StreamReader streamReader, Guid fileId, StreamReader? ownedStreamReader)
        {
            return CreateForMarkdownInternal(
                streamReader: streamReader,
                fileId: fileId,
                ownedStreamReader: ownedStreamReader,
                maxTokensPerChunk: 300,
                maxTokensPerSection: 4000);
        }

        /// <summary>
        /// Creates a SectionReader configured for Markdown content with custom token limits.
        /// </summary>
        /// <param name="stream">The stream to read content from.</param>
        /// <param name="fileId">The unique identifier of the knowledge file being processed.</param>
        /// <param name="maxTokensPerChunk">The maximum number of tokens per chunk.</param>
        /// <param name="maxTokensPerSection">The maximum number of tokens per section.</param>
        /// <returns>A configured SectionReader instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
        public SectionReader CreateForMarkdown(
            Stream stream,
            Guid fileId,
            int maxTokensPerChunk,
            int maxTokensPerSection)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            StreamReader streamReader = new StreamReader(stream);
            return CreateForMarkdownInternal(streamReader, fileId, ownedStreamReader: streamReader, maxTokensPerChunk, maxTokensPerSection);
        }

        /// <summary>
        /// Creates a SectionReader configured for Markdown content with custom token limits.
        /// </summary>
        /// <param name="streamReader">The stream reader to read content from.</param>
        /// <param name="fileId">The unique identifier of the knowledge file being processed.</param>
        /// <param name="maxTokensPerChunk">The maximum number of tokens per chunk.</param>
        /// <param name="maxTokensPerSection">The maximum number of tokens per section.</param>
        /// <returns>A configured SectionReader instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when streamReader is null.</exception>
        public SectionReader CreateForMarkdown(
            StreamReader streamReader,
            Guid fileId,
            int maxTokensPerChunk,
            int maxTokensPerSection)
        {
            return CreateForMarkdownInternal(
                streamReader: streamReader,
                fileId: fileId,
                ownedStreamReader: null,
                maxTokensPerChunk: maxTokensPerChunk,
                maxTokensPerSection: maxTokensPerSection);
        }

        /// <summary>
        /// Internal method to create a SectionReader with custom token limits and optional StreamReader ownership.
        /// </summary>
        /// <param name="streamReader">The stream reader to read content from.</param>
        /// <param name="fileId">The unique identifier of the knowledge file being processed.</param>
        /// <param name="ownedStreamReader">The StreamReader to be owned and disposed by the SectionReader, or null.</param>
        /// <param name="maxTokensPerChunk">The maximum number of tokens per chunk.</param>
        /// <param name="maxTokensPerSection">The maximum number of tokens per section.</param>
        /// <returns>A configured SectionReader instance.</returns>
        private SectionReader CreateForMarkdownInternal(
            StreamReader streamReader,
            Guid fileId,
            StreamReader? ownedStreamReader,
            int maxTokensPerChunk,
            int maxTokensPerSection)
        {
            return CreateForMarkdownInternal(
                streamReader: streamReader,
                fileId: fileId,
                ownedStreamReader: ownedStreamReader,
                maxTokensPerChunk: maxTokensPerChunk,
                maxTokensPerSection: maxTokensPerSection,
                lookaheadBufferSize: 100,
                standardDeviationMultiplier: 1.0);
        }

        /// <summary>
        /// Creates a SectionReader configured for Markdown content with full customization.
        /// </summary>
        /// <param name="streamReader">The stream reader to read content from.</param>
        /// <param name="fileId">The unique identifier of the knowledge file being processed.</param>
        /// <param name="maxTokensPerChunk">The maximum number of tokens per chunk.</param>
        /// <param name="maxTokensPerSection">The maximum number of tokens per section.</param>
        /// <param name="lookaheadBufferSize">The size of the lookahead buffer for statistical analysis.</param>
        /// <param name="standardDeviationMultiplier">The standard deviation multiplier for determining split thresholds.</param>
        /// <returns>A configured SectionReader instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when streamReader is null.</exception>
        public SectionReader CreateForMarkdown(
            StreamReader streamReader,
            Guid fileId,
            int maxTokensPerChunk,
            int maxTokensPerSection,
            int lookaheadBufferSize,
            double standardDeviationMultiplier)
        {
            return CreateForMarkdownInternal(
                streamReader: streamReader,
                fileId: fileId,
                ownedStreamReader: null,
                maxTokensPerChunk: maxTokensPerChunk,
                maxTokensPerSection: maxTokensPerSection,
                lookaheadBufferSize: lookaheadBufferSize,
                standardDeviationMultiplier: standardDeviationMultiplier);
        }

        /// <summary>
        /// Internal method to create a SectionReader with full customization and optional StreamReader ownership.
        /// </summary>
        /// <param name="streamReader">The stream reader to read content from.</param>
        /// <param name="fileId">The unique identifier of the knowledge file being processed.</param>
        /// <param name="ownedStreamReader">The StreamReader to be owned and disposed by the SectionReader, or null.</param>
        /// <param name="maxTokensPerChunk">The maximum number of tokens per chunk.</param>
        /// <param name="maxTokensPerSection">The maximum number of tokens per section.</param>
        /// <param name="lookaheadBufferSize">The size of the lookahead buffer for statistical analysis.</param>
        /// <param name="standardDeviationMultiplier">The standard deviation multiplier for determining split thresholds.</param>
        /// <returns>A configured SectionReader instance.</returns>
        private SectionReader CreateForMarkdownInternal(
            StreamReader streamReader,
            Guid fileId,
            StreamReader? ownedStreamReader,
            int maxTokensPerChunk,
            int maxTokensPerSection,
            int lookaheadBufferSize,
            double standardDeviationMultiplier)
        {
            if (streamReader == null)
                throw new ArgumentNullException(nameof(streamReader));

            ChunkingConfiguration chunkingConfig = new ChunkingConfiguration(
                tokenizer: _tokenizer,
                maxTokensPerChunk: maxTokensPerChunk,
                chunkStopSignals: ChunkStopSignals.Markdown);

            SectioningConfiguration sectioningConfig = new SectioningConfiguration(
                maxTokensPerSection: maxTokensPerSection,
                lookaheadBufferSize: lookaheadBufferSize,
                standardDeviationMultiplier: standardDeviationMultiplier,
                chunkStopSignals: ChunkStopSignals.Markdown);

            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(streamReader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfig);

            return new SectionReader(chunkReader, _embeddingService, sectioningConfig, _tokenizer, fileId, ownedStreamReader);
        }

        /// <summary>
        /// Creates a SectionReader with custom configurations.
        /// </summary>
        /// <param name="stream">The stream to read content from.</param>
        /// <param name="fileId">The unique identifier of the knowledge file being processed.</param>
        /// <param name="chunkingConfiguration">The configuration for chunking operations.</param>
        /// <param name="sectioningConfiguration">The configuration for sectioning operations.</param>
        /// <param name="textSegmentSplitters">The text segment splitters to use for segmentation.</param>
        /// <returns>A configured SectionReader instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        public SectionReader Create(
            Stream stream,
            Guid fileId,
            ChunkingConfiguration chunkingConfiguration,
            SectioningConfiguration sectioningConfiguration,
            string[] textSegmentSplitters)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            StreamReader streamReader = new StreamReader(stream);
            return CreateInternal(streamReader, fileId, chunkingConfiguration, sectioningConfiguration, textSegmentSplitters, ownedStreamReader: streamReader);
        }

        /// <summary>
        /// Creates a SectionReader with custom configurations.
        /// </summary>
        /// <param name="streamReader">The stream reader to read content from.</param>
        /// <param name="fileId">The unique identifier of the knowledge file being processed.</param>
        /// <param name="chunkingConfiguration">The configuration for chunking operations.</param>
        /// <param name="sectioningConfiguration">The configuration for sectioning operations.</param>
        /// <param name="textSegmentSplitters">The text segment splitters to use for segmentation.</param>
        /// <returns>A configured SectionReader instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        public SectionReader Create(
            StreamReader streamReader,
            Guid fileId,
            ChunkingConfiguration chunkingConfiguration,
            SectioningConfiguration sectioningConfiguration,
            string[] textSegmentSplitters)
        {
            return CreateInternal(streamReader, fileId, chunkingConfiguration, sectioningConfiguration, textSegmentSplitters, ownedStreamReader: null);
        }

        /// <summary>
        /// Internal method to create a SectionReader with custom configurations and optional StreamReader ownership.
        /// </summary>
        /// <param name="streamReader">The stream reader to read content from.</param>
        /// <param name="fileId">The unique identifier of the knowledge file being processed.</param>
        /// <param name="chunkingConfiguration">The configuration for chunking operations.</param>
        /// <param name="sectioningConfiguration">The configuration for sectioning operations.</param>
        /// <param name="textSegmentSplitters">The text segment splitters to use for segmentation.</param>
        /// <param name="ownedStreamReader">The StreamReader to be owned and disposed by the SectionReader, or null.</param>
        /// <returns>A configured SectionReader instance.</returns>
        private SectionReader CreateInternal(
            StreamReader streamReader,
            Guid fileId,
            ChunkingConfiguration chunkingConfiguration,
            SectioningConfiguration sectioningConfiguration,
            string[] textSegmentSplitters,
            StreamReader? ownedStreamReader)
        {
            if (streamReader == null)
                throw new ArgumentNullException(nameof(streamReader));
            if (chunkingConfiguration == null)
                throw new ArgumentNullException(nameof(chunkingConfiguration));
            if (sectioningConfiguration == null)
                throw new ArgumentNullException(nameof(sectioningConfiguration));
            if (textSegmentSplitters == null)
                throw new ArgumentNullException(nameof(textSegmentSplitters));

            TextSegmentReader textSegmentReader = TextSegmentReader.Create(streamReader, textSegmentSplitters);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfiguration);

            return new SectionReader(chunkReader, _embeddingService, sectioningConfiguration, _tokenizer, fileId, ownedStreamReader);
        }
    }
}