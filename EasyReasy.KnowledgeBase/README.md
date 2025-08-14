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
Guid fileId = Guid.NewGuid(); // The ID of the knowledge file being processed

// Create a section reader with sensible defaults
SectionReader sectionReader = factory.CreateForMarkdown(stream, fileId, maxTokensPerChunk: 100, maxTokensPerSection: 1000);

// Read sections
int sectionIndex = 0;
await foreach (List<KnowledgeFileChunk> chunks in sectionReader.ReadSectionsAsync())
{
    KnowledgeFileSection section = KnowledgeFileSection.CreateFromChunks(chunks, fileId, sectionIndex);
    Console.WriteLine($"Section: {section.ToString()}");
    sectionIndex++;
}
```

### Manual Configuration
```csharp
// For more control over the chunking process
Guid fileId = Guid.NewGuid(); // The ID of the knowledge file being processed
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
SectionReader sectionReader = new SectionReader(chunkReader, embeddingService, sectioningConfig, tokenizer, fileId);

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
- Constructor: `(SegmentBasedChunkReader chunkReader, IEmbeddingService embeddings, SectioningConfiguration config, ITokenizer tokenizer, Guid fileId)`
- `ReadSectionsAsync()`: Returns `IAsyncEnumerable<List<KnowledgeFileChunk>>`

**SectionReaderFactory**
- Constructor: `(IEmbeddingService embeddingService, ITokenizer tokenizer)`
- `CreateForMarkdown(Stream stream, Guid fileId, int maxTokensPerChunk, int maxTokensPerSection)`: Quick setup for markdown documents

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
- `CreateFromChunks(List<KnowledgeFileChunk> chunks, Guid fileId, int sectionIndex)`: Creates section from chunks
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

## Storage System

The EasyReasy.KnowledgeBase library provides a comprehensive storage abstraction for managing knowledge files, chunks, sections, and vector embeddings. The storage system is designed with a clean separation of concerns, allowing you to implement different storage backends while maintaining a consistent API.

### Storage Architecture

The storage system follows a **layered architecture** with clear interfaces:

```
IKnowledgeStore (Main Interface)
├── IFileStore (File Management)
├── IChunkStore (Chunk Storage)
├── ISectionStore (Section Storage)
└── IVectorStore (Vector Embeddings)
```

### Quick Start with Storage

```csharp
// Create storage implementations (you provide these)
IFileStore fileStore = new YourFileStore();
IChunkStore chunkStore = new YourChunkStore();
ISectionStore sectionStore = new YourSectionStore();
IVectorStore vectorStore = new YourVectorStore();

// Create the main knowledge store
KnowledgeStore knowledgeStore = new KnowledgeStore(fileStore, chunkStore, sectionStore);

// Initialize the store
await knowledgeStore.InitializeAsync();

// Store a knowledge file
KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "document.md", contentHash);
Guid fileId = await knowledgeStore.Files.AddAsync(file);

// Store chunks with embeddings
foreach (KnowledgeFileChunk chunk in chunks)
{
    await knowledgeStore.Chunks.AddAsync(chunk);
    if (chunk.Embedding != null)
    {
        await vectorStore.AddAsync(chunk.Id, chunk.Embedding);
    }
}

// Store sections
foreach (KnowledgeFileSection section in sections)
{
    await knowledgeStore.Sections.AddAsync(section);
    if (section.Embedding != null)
    {
        await vectorStore.AddAsync(section.Id, section.Embedding);
    }
}

// Persist changes
await knowledgeStore.PersistAsync();
```

### Storage Interfaces

#### IKnowledgeStore
The main interface that provides access to all storage components:

```csharp
public interface IKnowledgeStore
{
    IFileStore Files { get; }
    IChunkStore Chunks { get; }
    ISectionStore Sections { get; }
}
```

#### IFileStore
Manages knowledge file metadata:

```csharp
// Add a new knowledge file
Guid fileId = await fileStore.AddAsync(new KnowledgeFile(id, name, hash));

// Retrieve a file
KnowledgeFile? file = await fileStore.GetAsync(fileId);

// Check if file exists
bool exists = await fileStore.ExistsAsync(fileId);

// Get all files
IEnumerable<KnowledgeFile> allFiles = await fileStore.GetAllAsync();

// Update file metadata
await fileStore.UpdateAsync(updatedFile);

// Delete a file
bool deleted = await fileStore.DeleteAsync(fileId);
```

#### IChunkStore
Manages individual content chunks:

```csharp
// Add a chunk
await chunkStore.AddAsync(new KnowledgeFileChunk(id, sectionId, index, content, embedding));

// Get chunk by ID
KnowledgeFileChunk? chunk = await chunkStore.GetAsync(chunkId);

// Get chunk by index within section
KnowledgeFileChunk? chunk = await chunkStore.GetByIndexAsync(sectionId, chunkIndex);

// Navigate chunks sequentially
KnowledgeFileChunk? next = await chunkStore.GetNextAsync(sectionId, currentIndex);
KnowledgeFileChunk? previous = await chunkStore.GetPreviousAsync(sectionId, currentIndex);

// Delete all chunks for a file
bool deleted = await chunkStore.DeleteByFileAsync(fileId);
```

#### ISectionStore
Manages sections containing multiple chunks:

```csharp
// Add a section
await sectionStore.AddAsync(new KnowledgeFileSection(id, fileId, index, chunks, summary, embedding));

// Get section by ID
KnowledgeFileSection? section = await sectionStore.GetAsync(sectionId);

// Get section by index within file
KnowledgeFileSection? section = await sectionStore.GetByIndexAsync(fileId, sectionIndex);

// Navigate sections sequentially
KnowledgeFileSection? next = await sectionStore.GetNextAsync(fileId, currentIndex);
KnowledgeFileSection? previous = await sectionStore.GetPreviousAsync(fileId, currentIndex);

// Delete all sections for a file
bool deleted = await sectionStore.DeleteByFileAsync(fileId);
```

#### IVectorStore
Manages vector embeddings for similarity search:

```csharp
// Add a vector
await vectorStore.AddAsync(entityId, embedding);

// Remove a vector
await vectorStore.RemoveAsync(entityId);

// Search for similar vectors
IEnumerable<Guid> similarIds = await vectorStore.SearchAsync(queryVector, maxResults);
```

### Storage Models

#### KnowledgeFile
Represents a knowledge file with metadata:

```csharp
public class KnowledgeFile
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public byte[] Hash { get; set; } // Content hash for integrity
}
```

#### KnowledgeFileChunk
Represents a chunk of content with optional embedding:

```csharp
public class KnowledgeFileChunk : IVectorObject
{
    public Guid Id { get; set; }
    public Guid SectionId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; }
    public float[]? Embedding { get; set; }
    
    public float[] GetVector() => Embedding ?? Array.Empty<float>();
    public bool ContainsVector() => Embedding != null;
}
```

#### KnowledgeFileSection
Represents a section containing multiple chunks:

```csharp
public class KnowledgeFileSection : IVectorObject
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public int SectionIndex { get; set; }
    public string? Summary { get; set; }
    public List<KnowledgeFileChunk> Chunks { get; set; }
    public float[]? Embedding { get; set; }
    
    public static KnowledgeFileSection CreateFromChunks(List<KnowledgeFileChunk> chunks, Guid fileId, int sectionIndex);
    public float[] GetVector() => Embedding ?? Array.Empty<float>();
    public bool ContainsVector() => Embedding != null;
    public override string ToString() => Combined content of all chunks;
}
```

### Storage Implementation Patterns

#### File-Based Storage
```csharp
public class FileBasedFileStore : IFileStore
{
    private readonly string _basePath;
    
    public async Task<Guid> AddAsync(KnowledgeFile file)
    {
        string filePath = Path.Combine(_basePath, $"{file.Id}.json");
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(file));
        return file.Id;
    }
    
    // Implement other methods...
}
```

#### Database Storage
```csharp
public class DatabaseChunkStore : IChunkStore
{
    private readonly IDbConnection _connection;
    
    public async Task AddAsync(KnowledgeFileChunk chunk)
    {
        const string sql = "INSERT INTO chunks (id, section_id, chunk_index, content, embedding) VALUES (@Id, @SectionId, @ChunkIndex, @Content, @Embedding)";
        await _connection.ExecuteAsync(sql, chunk);
    }
    
    // Implement other methods...
}
```

#### In-Memory Vector Store
```csharp
public class InMemoryVectorStore : IVectorStore
{
    private readonly Dictionary<Guid, float[]> _vectors = new();
    
    public async Task AddAsync(Guid guid, float[] vector)
    {
        _vectors[guid] = vector;
        await Task.CompletedTask;
    }
    
    public async Task<IEnumerable<Guid>> SearchAsync(float[] queryVector, int maxResultsCount)
    {
        var similarities = _vectors.Select(kvp => new
        {
            Id = kvp.Key,
            Similarity = ConfidenceMath.CosineSimilarity(queryVector, kvp.Value)
        });
        
        return similarities
            .OrderByDescending(x => x.Similarity)
            .Take(maxResultsCount)
            .Select(x => x.Id);
    }
    
    // Implement other methods...
}
```

### Storage Best Practices

#### 1. Transaction Management
```csharp
// Ensure atomic operations across multiple stores
await knowledgeStore.InitializeAsync();
try
{
    await knowledgeStore.Files.AddAsync(file);
    foreach (var chunk in chunks)
    {
        await knowledgeStore.Chunks.AddAsync(chunk);
        await vectorStore.AddAsync(chunk.Id, chunk.Embedding);
    }
    await knowledgeStore.PersistAsync();
}
catch
{
    // Handle rollback if needed
    throw;
}
```

#### 2. Batch Operations
```csharp
// For better performance with large datasets
public async Task AddChunksBatchAsync(IEnumerable<KnowledgeFileChunk> chunks)
{
    var tasks = chunks.Select(chunk => AddAsync(chunk));
    await Task.WhenAll(tasks);
}
```

#### 3. Caching Strategies
```csharp
public class CachedSectionStore : ISectionStore
{
    private readonly ISectionStore _innerStore;
    private readonly IMemoryCache _cache;
    
    public async Task<KnowledgeFileSection?> GetAsync(Guid sectionId)
    {
        string cacheKey = $"section:{sectionId}";
        if (_cache.TryGetValue(cacheKey, out KnowledgeFileSection? cached))
            return cached;
            
        var section = await _innerStore.GetAsync(sectionId);
        if (section != null)
            _cache.Set(cacheKey, section, TimeSpan.FromMinutes(30));
            
        return section;
    }
}
```

#### 4. Vector Store Optimization
```csharp
// Use approximate nearest neighbor search for large vector collections
public class OptimizedVectorStore : IVectorStore
{
    private readonly HnswIndex<float[]> _index; // Example using HNSW
    
    public async Task<IEnumerable<Guid>> SearchAsync(float[] queryVector, int maxResultsCount)
    {
        var results = _index.Search(queryVector, maxResultsCount);
        return results.Select(r => r.Id);
    }
}
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