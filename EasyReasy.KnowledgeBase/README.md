# EasyReasy.KnowledgeBase

[← Back to EasyReasy System](../README.md)

[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.KnowledgeBase-blue.svg)](https://www.nuget.org/packages/EasyReasy.KnowledgeBase)

A powerful .NET library for processing and intelligently chunking knowledge documents using embedding-based similarity analysis. Designed for RAG (Retrieval-Augmented Generation) systems that need to handle large documents and books efficiently.

## Key Features

- **🧠 Smart Sectioning**: Uses embedding similarity and statistical analysis to group related chunks
- **📊 Adaptive Thresholds**: Automatically determines section boundaries using standard deviation analysis  
- **💾 Memory Efficient**: Streams large documents without loading everything into memory
- **🎯 Progressive Strictness**: Becomes more selective about section breaks as sections approach max size
- **📝 Markdown Aware**: Respects markdown structure (headers, code blocks) for natural boundaries
- **⚡ High Performance**: Three-tier streaming architecture optimized for large documents

## Quick Start

### Simple Section Reading
```csharp
// Using the factory for easy setup
SectionReaderFactory factory = new SectionReaderFactory(embeddingService, tokenizer);
using Stream stream = File.OpenRead("document.md");

// Create a section reader with sensible defaults
SectionReader sectionReader = factory.CreateForMarkdown(stream, maxTokensPerChunk: 100, maxTokensPerSection: 1000);

// Read sections
await foreach (List<KnowledgeFileChunk> chunks in sectionReader.ReadSectionsAsync())
{
    KnowledgeFileSection section = KnowledgeFileSection.CreateFromChunks(chunks);
    Console.WriteLine($"Section: {section.ToString()}");
}
```

### Manual Configuration
```csharp
// For more control over the chunking process
ChunkingConfiguration chunkingConfig = new ChunkingConfiguration(tokenizer, maxTokensPerChunk: 100, ChunkStopSignals.Markdown);
SectioningConfiguration sectioningConfig = new SectioningConfiguration(
    maxTokensPerSection: 1000,
    lookaheadBufferSize: 200,
    standardDeviationMultiplier: 1.0,
    minimumTokensPerSection: 50,
    chunkStopSignals: ChunkStopSignals.Markdown);

using StreamReader reader = new StreamReader(stream);
TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfig);
SectionReader sectionReader = new SectionReader(chunkReader, embeddingService, sectioningConfig, tokenizer);

await foreach (List<KnowledgeFileChunk> chunks in sectionReader.ReadSectionsAsync())
{
    // Process sections...
}
```

## Architecture Overview

The system uses a **three-tier streaming architecture** for efficient document processing:

### 1. Text Segment Reader
- Reads the smallest meaningful text units (sentences, lines, paragraphs)
- Handles different text formats (markdown, plain text)
- Memory efficient streaming

### 2. Chunk Reader  
- Combines segments into chunks based on token limits
- Respects **stop signals** (markdown headers, code blocks) for natural boundaries
- Configurable chunk sizes and stop conditions

### 3. Section Reader
- Groups chunks into sections using **embedding similarity analysis**
- Uses **statistical thresholds** (standard deviation) instead of fixed similarity values
- **Progressive strictness**: Becomes more selective as sections approach size limits
- **Minimum constraints**: Prevents tiny sections from being created

## How Smart Sectioning Works

1. **Lookahead Analysis**: Maintains a buffer of upcoming chunks (~200 by default)
2. **Statistical Thresholding**: Calculates similarity distribution and uses `mean - (multiplier × std_deviation)` as split threshold
3. **Progressive Strictness**: After 75% section capacity, splitting likelihood increases quadratically
4. **Minimum Constraints**: Ensures sections have meaningful content (minimum chunks and tokens)
5. **Stop Signal Awareness**: Considers markdown structure when making splitting decisions

## API Reference

### Core Configuration Classes

**ChunkingConfiguration**
```csharp
new ChunkingConfiguration(
    ITokenizer tokenizer,
    int maxTokensPerChunk = 300,
    string[]? chunkStopSignals = null)
```
- `MaxTokensPerChunk`: Maximum tokens per chunk
- `ChunkStopSignals`: Signals that force chunk boundaries (e.g., `ChunkStopSignals.Markdown`)

**SectioningConfiguration**  
```csharp
new SectioningConfiguration(
    int maxTokensPerSection = 4000,
    int lookaheadBufferSize = 100,
    double standardDeviationMultiplier = 1.0,
    double minimumSimilarityThreshold = 0.65,
    double tokenStrictnessThreshold = 0.75,
    int minimumChunksPerSection = 2,
    int minimumTokensPerSection = 50,
    string[]? chunkStopSignals = null)
```

### Readers

**TextSegmentReader**
- `CreateForMarkdown(StreamReader reader)`: Creates reader optimized for markdown
- `ReadNextTextSegmentAsync()`: Returns next text segment or null

**SegmentBasedChunkReader**
- Constructor: `(TextSegmentReader segmentReader, ChunkingConfiguration config)`
- `ReadNextChunkContentAsync()`: Returns next chunk as string or null

**SectionReader**
- Constructor: `(SegmentBasedChunkReader chunkReader, IEmbeddingService embeddings, SectioningConfiguration config, ITokenizer tokenizer)`
- `ReadSectionsAsync()`: Returns `IAsyncEnumerable<List<KnowledgeFileChunk>>`

**SectionReaderFactory**
- Constructor: `(IEmbeddingService embeddingService, ITokenizer tokenizer)`
- `CreateForMarkdown(Stream stream, int maxTokensPerChunk, int maxTokensPerSection)`: Quick setup for markdown documents

### Stop Signals

**ChunkStopSignals**
- `ChunkStopSignals.Markdown`: Pre-configured signals for markdown (headers, code blocks, bold text)
- Custom arrays can be provided for other document types

### Models

**KnowledgeFileChunk**
- `Id`: Guid
- `Content`: string  
- `Embedding`: float[]

**KnowledgeFileSection**
- `CreateFromChunks(List<KnowledgeFileChunk> chunks)`: Creates section from chunks
- `ToString()`: Returns combined content
- `ToString(string separator)`: Returns content with custom separator

### Confidence Rating Utilities

**ConfidenceMath** (Static Class)
- `CosineSimilarity(float[] a, float[] b)`: Calculate similarity between vectors
- `UpdateCentroidInPlace(float[] centroid, float[] nextVector, int countBefore)`: Update running average
- `NormalizeVector(float[] vector)`: L2 normalization
- `CalculateStandardDeviation(double[] values, bool sample = false)`: Statistical analysis

### Interfaces

**IEmbeddingService**
- `EmbedAsync(string text, CancellationToken cancellationToken)`: Generate embeddings

**ITokenizer**  
- `CountTokens(string text)`: Count tokens in text

## Configuration Tips

### For Technical Documentation
```csharp
SectioningConfiguration config = new SectioningConfiguration(
    maxTokensPerSection: 800,
    standardDeviationMultiplier: 0.8, // More aggressive splitting
    minimumTokensPerSection: 100,
    chunkStopSignals: ChunkStopSignals.Markdown);
```

### For Narrative Content
```csharp  
SectioningConfiguration config = new SectioningConfiguration(
    maxTokensPerSection: 1200,
    standardDeviationMultiplier: 1.2, // More lenient splitting  
    minimumTokensPerSection: 75);
```

### For Large Books
```csharp
SectioningConfiguration config = new SectioningConfiguration(
    maxTokensPerSection: 1500,
    lookaheadBufferSize: 300, // Larger lookahead for better statistics
    tokenStrictnessThreshold: 0.65); // Earlier progressive strictness
```

## Dependencies

- **.NET 8.0+**: Modern async/await patterns and performance features
- **Your embedding service**: Implement `IEmbeddingService` 
- **Your tokenizer**: Implement `ITokenizer` (or use EasyReasy.KnowledgeBase.BertTokenization)

## Performance Characteristics

- **Memory**: O(lookahead_buffer_size) - typically ~50-500 chunks in memory
- **Processing**: Streams through documents of any size
- **API Calls**: One embedding call per chunk (cached for statistical analysis)
- **Throughput**: Optimized for large documents (tested with full books)

## License

MIT