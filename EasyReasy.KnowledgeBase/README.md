# EasyReasy.KnowledgeBase

[← Back to EasyReasy System](../README.md)

[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.KnowledgeBase-blue.svg)](https://www.nuget.org/packages/EasyReasy.KnowledgeBase)

A powerful .NET library for processing and intelligently chunking knowledge documents using embedding-based similarity analysis. Designed for RAG (Retrieval-Augmented Generation) systems that need to handle large documents and books efficiently.

## Key Features

- **🧠 Smart AI Semantic Sectioning**: Uses embedding similarity and statistical analysis to intelligently group related chunks into sections
- **📊 Adaptive Thresholds**: Automatically determines section boundaries using standard deviation analysis of the embedding semantic similarity instead of fixed similarity values
- **💾 Memory Efficient**: Three-tier streaming architecture that processes large documents without loading everything into memory. No matter how small or large a file is, it can be chunked and sectioned with very low memory usage.
- **🎯 Progressive Strictness**: Becomes more selective about section breaks as sections approach maximum size
- **📝 Document Format Aware**: Respects document structure (markdown headers, code blocks) for natural boundaries, with support for custom document types
- **🔍 Intelligent Search**: Vector-based similarity search with comprehensive confidence ratings and relevance metrics
- **🤖 AI Generation**: Built-in services for summarization, question generation, and contextualization using one-shot LLM generation

## No Forced Dependencies
This package is built with the philosophy that no dependencies should be required. This core library provides all the interfaces and logic for how they interact but how you implement them is up to you. Or if you don't want to implement them yourself all of them are already readily implemented for you in separate packages. You can choose exactly which you want to use and which you want to implement yourself.

### Available Packages for Implementations

- **EasyReasy.KnowledgeBase**: Core library with interfaces and models
- **EasyReasy.KnowledgeBase.Storage.Sqlite**: SQLite-based storage implementation
- **EasyReasy.KnowledgeBase.Storage.IntegratedVectorStore**: Vector storage integration
- **EasyReasy.VectorStorage**: High-performance vector storage and similarity search
- **EasyReasy.KnowledgeBase.BertTokenization**: BERT-based tokenization
- **EasyReasy.KnowledgeBase.OllamaGeneration**: Ollama integration for embeddings and generation

## Quick Start

### Creating a Searchable Knowledge Base
> [!NOTE] In the example below a lot of implementations from other packages are used.
> This is because this main package (EasyReasy.KnowledgeBase) mostly contains interfaces for things.
> Implementations are then in other packages so that there are no forced dependencies and you can choose yourself if you want to
> use the provided implementations (from the provided additional packages) or if you want to create your own.
```csharp
// Set up your services using provided implementations
// Note: These implementations are in separate packages - see Dependencies section below
BertTokenizer tokenizer = await BertTokenizer.CreateAsync();
EasyReasyOllamaEmbeddingService embeddingService = await EasyReasyOllamaEmbeddingService.CreateAsync(
    baseUrl: "https://your-ollama-server.com",
    apiKey: "your-api-key",
    modelName: "nomic-embed-text");

// Create storage components using provided implementations
// Note: These implementations are in separate packages - you can also implement your own
IKnowledgeStore knowledgeStore = await SqliteKnowledgeStore.CreateAsync("knowledge.db");
CosineVectorStore cosineVectorStore = new CosineVectorStore(embeddingService.Dimensions);
IKnowledgeVectorStore chunksVectorStore = new EasyReasyVectorStore(cosineVectorStore);

// Create the searchable knowledge store
ISearchableKnowledgeStore searchableKnowledgeStore = new SearchableKnowledgeStore(knowledgeStore, chunksVectorStore);

// Create the searchable knowledge base
ISearchableKnowledgeBase knowledgeBase = new SearchableKnowledgeBase(
    searchableKnowledgeStore, 
    embeddingService, 
    tokenizer);

// Index documents using a file source provider
IFileSourceProvider fileSourceProvider = new YourFileSourceProvider();
IIndexer indexer = knowledgeBase.CreateIndexer();

foreach (IFileSource fileSource in await fileSourceProvider.GetAllFilesAsync())
{
    await indexer.ConsumeAsync(fileSource);
}

// Search for relevant content
IKnowledgeBaseSearchResult result = await knowledgeBase.SearchAsync("your query", maxSearchResultsCount: 10);
if (result.WasSuccess)
{
    // Cast to concrete type to access detailed results
    if (result is KnowledgeBaseSearchResult searchResult)
    {
        foreach (RelevanceRatedEntry<KnowledgeFileSection> section in searchResult.RelevantSections)
        {
            Console.WriteLine($"Relevance: {section.Relevance.RelevanceScore}");
            Console.WriteLine($"Content: {section.Item.ToString()}");
        }
    }
    
    // Or use the context string for LLM input
    string contextString = result.GetAsContextString();
    Console.WriteLine(contextString);
}
```

### Simple Section Reading
```csharp
// Set up services using provided implementations
BertTokenizer tokenizer = await BertTokenizer.CreateAsync();
EasyReasyOllamaEmbeddingService embeddingService = await EasyReasyOllamaEmbeddingService.CreateAsync(
    baseUrl: "https://your-ollama-server.com",
    apiKey: "your-api-key",
    modelName: "nomic-embed-text");

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

// Clean up resources
embeddingService.Dispose();
```

### Manual Configuration
```csharp
// Set up services using provided implementations
BertTokenizer tokenizer = await BertTokenizer.CreateAsync();
EasyReasyOllamaEmbeddingService embeddingService = await EasyReasyOllamaEmbeddingService.CreateAsync(
    baseUrl: "https://your-ollama-server.com",
    apiKey: "your-api-key",
    modelName: "nomic-embed-text");

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

// Clean up resources
embeddingService.Dispose();
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

**ITextSegmentReader**
- `ReadNextTextSegmentAsync(CancellationToken cancellationToken)`: Returns next text segment or null
- Generic interface for text segmentation capabilities

**SegmentBasedChunkReader**
- Constructor: `(TextSegmentReader segmentReader, ChunkingConfiguration config)`
- `ReadNextChunkContentAsync()`: Returns next chunk as string or null

**IKnowledgeChunkReader**
- `ReadNextChunkContentAsync(CancellationToken cancellationToken)`: Returns next chunk as string or null
- Interface for reading chunks of content from streams

**SectionReader**
- Constructor: `(SegmentBasedChunkReader chunkReader, IEmbeddingService embeddings, SectioningConfiguration config, ITokenizer tokenizer, Guid fileId)`
- `ReadSectionsAsync()`: Returns `IAsyncEnumerable<List<KnowledgeFileChunk>>`

**IKnowledgeSectionReader**
- `ReadSectionsAsync(CancellationToken cancellationToken)`: Returns `IAsyncEnumerable<List<KnowledgeFileChunk>>`
- Interface for reading sections by grouping chunks based on content similarity
- Implements `IDisposable`

**SectionReaderFactory**
- Constructor: `(IEmbeddingService embeddingService, ITokenizer tokenizer)`
- `CreateForMarkdown(Stream stream, Guid fileId, int maxTokensPerChunk, int maxTokensPerSection)`: Quick setup for markdown documents

**ITokenReader**
- `ReadNextTokens(int tokenCount)`: Reads next specified number of tokens
- `PeekNextTokens(int tokenCount)`: Peeks at next tokens without consuming them
- `SeekBackward(int tokenCount)`: Seeks backward in token buffer
- `CurrentPosition`: Gets current position in token stream
- `TotalTokensRead`: Gets total tokens read so far
- `HasMoreTokens`: Checks if more tokens are available
- Provides streaming tokenization with forward and backward buffer support

### Text Segmentation

**TextSegmentSplitters**
- `TextSegmentSplitters.Markdown`: Predefined break strings optimized for Markdown content
- Includes heading markers, paragraph breaks, list items, code blocks, line breaks, and sentence endings
- Ordered by preference with more specific patterns first

### Stop Signals

**ChunkStopSignals**
- `ChunkStopSignals.Markdown`: Pre-configured signals for markdown (headers, code blocks, bold text)
- Custom arrays can be provided for other document types

### Models

**KnowledgeFile**
- `Id`: Guid - Unique identifier for the knowledge file
- `Name`: string - Name of the knowledge file
- `Hash`: byte[] - Content hash for integrity verification
- Constructor: `(Guid id, string name, byte[] hash)` - Creates knowledge file with metadata

**KnowledgeFileChunk**
- `Id`: Guid
- `SectionId`: Guid
- `ChunkIndex`: int
- `Content`: string  
- `Embedding`: float[]
- `Vector()`: Returns the embedding vector
- `ContainsVector()`: Returns true if embedding is available

**KnowledgeFileSection**
- `Id`: Guid
- `FileId`: Guid
- `SectionIndex`: int
- `Summary`: string?
- `AdditionalContext`: string?
- `Chunks`: List<KnowledgeFileChunk>
- `Embedding`: float[]
- `CreateFromChunks(List<KnowledgeFileChunk> chunks, Guid fileId, int sectionIndex)`: Creates section from chunks
- `Vector()`: Returns the embedding vector
- `ContainsVector()`: Returns true if embedding is available
- `ToString()`: Returns combined content
- `ToString(string separator)`: Returns content with custom separator

### Generation Services

**IEmbeddingService**
- `ModelName`: string - Name of the embedding model
- `Dimensions`: int - Number of dimensions in the embedding vectors
- `EmbedAsync(string text, CancellationToken cancellationToken)`: Generate embeddings

**ISummarizationService**
- `SummarizeAsync(string text, CancellationToken cancellationToken)`: Generate summaries of text content

**IQuestionGenerationService**
- `GenerateQuestionsAsync(string text, CancellationToken cancellationToken)`: Generate synthetic questions from text content

**IContextualizationService**
- `ContextualizeAsync(string textSnippet, string surroundingContent, CancellationToken cancellationToken)`: Provide contextual information for text snippets

**IOneShotService**
- `ProcessAsync(string systemPrompt, string userInput, CancellationToken cancellationToken)`: Perform one-shot text processing tasks

### Generation Service Implementations

**OneShotServiceBase** (Abstract Base Class)
- `OneShotService`: Protected property for underlying one-shot service
- `ProcessAsync(string systemPrompt, string userInput, CancellationToken cancellationToken)`: Protected method for processing
- Provides consistent foundation for services built on top of IOneShotService

**QuestionGenerationService** : OneShotServiceBase, IQuestionGenerationService
- Constructor: `(IOneShotService oneShotService, string? systemPrompt = null)`
- `GenerateQuestionsAsync(string text, CancellationToken cancellationToken)`: Generates 3-5 diverse questions
- Uses default system prompt for factual, conceptual, and application-based questions
- Includes retry logic with ListParser for robust question extraction

**SummarizationService** : OneShotServiceBase, ISummarizationService
- Constructor: `(IOneShotService oneShotService, string? systemPrompt = null)`
- `SummarizeAsync(string text, CancellationToken cancellationToken)`: Generates concise 2-3 sentence summaries
- Uses default system prompt focused on document retrieval and search

**ContextualizationService** : OneShotServiceBase, IContextualizationService
- Constructor: `(IOneShotService oneShotService, string? systemPrompt = null)`
- `ContextualizeAsync(string textSnippet, string surroundingContent, CancellationToken cancellationToken)`: Provides contextual information
- Uses default system prompt for explaining snippet's role and significance

### Generation Utilities

**ListParser** (Static Class)
- `ParseList(string text)`: Parses lists from text content
- Handles numbered lists (1., 2., etc.), bullet points (-, *), and plain text lists
- Removes list markers and returns clean list of strings
- Returns null for invalid or empty lists
- Used by QuestionGenerationService for robust question extraction

### Search Interfaces

**ISearchableKnowledgeBase**
- `CreateIndexer(IEmbeddingService? customEmbeddingService)`: Create an indexer for adding documents
- `SearchAsync(string query, int? maxSearchResultsCount, CancellationToken cancellationToken)`: Search for relevant content

**IKnowledgeBaseSearchResult**
- `WasSuccess`: bool - Whether the search was successful
- `CanBeRetried`: bool - Whether the search can be retried
- `ShouldBeRetried`: bool - Whether the search should be retried
- `ErrorMessage`: string? - Error message if search failed
- `GetAsContextString()`: string - Returns formatted context string for LLM input

### Search Implementations

**SearchableKnowledgeStore** : ISearchableKnowledgeStore
- Constructor: `(IFileStore fileStore, ISectionStore sectionStore, IChunkStore chunkStore, IKnowledgeVectorStore chunksVectorStore)`
- Constructor: `(IKnowledgeStore knowledgeStore, IKnowledgeVectorStore chunksVectorStore)`
- `Files`: IFileStore - Access to file storage
- `Sections`: ISectionStore - Access to section storage  
- `Chunks`: IChunkStore - Access to chunk storage
- `GetChunksVectorStore()`: IKnowledgeVectorStore - Vector store for chunk searches
- Wraps basic knowledge store with vector search capabilities

**KnowledgeBaseSearchResult** : IKnowledgeBaseSearchResult
- Constructor: `(IReadOnlyList<RelevanceRatedEntry<KnowledgeFileSection>> relevantSections, string query, bool wasSuccess = true, bool canBeRetried = false, bool shouldBeRetried = false, string? errorMessage = null)`
- `WasSuccess`: bool - Whether the search was successful
- `CanBeRetried`: bool - Whether the search can be retried
- `ShouldBeRetried`: bool - Whether the search should be retried
- `ErrorMessage`: string? - Error message if search failed
- `RelevantSections`: IReadOnlyList<RelevanceRatedEntry<KnowledgeFileSection>> - Relevant sections with metrics
- `Query`: string - Original search query
- `CreateError(string query, string errorMessage, bool canBeRetried = false, bool shouldBeRetried = false)`: Static method to create error results
- `GetAsContextString()`: string - Formats results as context string for LLM input

**KnowledgeVector** : IKnowledgeVector
- Constructor: `(Guid id, float[] vector)`
- `Id`: Guid - Unique identifier for the knowledge vector
- `Vector()`: float[] - Returns the vector representation
- `ContainsVector()`: bool - Returns true if vector is available
- Concrete implementation of IKnowledgeVector with vector data

### Search Factories

**IKnowledgeBaseFactory<T>**
- `CreateKnowledgebaseAsync()`: Task<T> - Creates and returns a knowledge base of type T
- Generic factory interface for creating different types of knowledge bases

### Indexing Interfaces

**IIndexer**
- `IndexAsync(IFileSource fileSource, CancellationToken cancellationToken)`: Index documents from a file source

**IFileSource**
- `Name`: string - Name of the file source
- `GetContentAsync(CancellationToken cancellationToken)`: Get the content of the file

**IFileSourceProvider**
- `GetFileSourcesAsync(CancellationToken cancellationToken)`: Get available file sources

### Indexing Implementations

**KnowledgeBaseIndexer** : IIndexer
- Constructor: `(ISearchableKnowledgeStore searchableKnowledgeStore, IEmbeddingService embeddingService, ITokenizer tokenizer, int maxTokensPerChunk = 100, int maxTokensPerSection = 1000)`
- `ConsumeAsync(IFileSource fileSource)`: Indexes file content into knowledge base
- **Features:**
  - Content hash verification for duplicate detection
  - Automatic cleanup of old content when file changes
  - Chunk and section creation with embeddings
  - Vector store integration for similarity search
  - Markdown-optimized processing using SectionReaderFactory

### Confidence Rating Utilities

**ConfidenceMath** (Static Class)
- **Vector Operations:**
  - `DotProduct(float[] a, float[] b)`: Calculate dot product of two vectors
  - `VectorNorm(float[] v)`: Calculate L2 norm (magnitude) of a vector
  - `NormalizeVector(float[] v)`: Normalize vector to unit length (L2 normalization)
  - `NormalizeVectorInPlace(float[] v)`: Normalize vector in-place
  - `CosineSimilarity(float[] a, float[] b)`: Calculate cosine similarity between vectors (-1 to 1)
  - `CosineSimilarityPreNormalized(float[] a, float[] b)`: Fast cosine similarity for pre-normalized vectors
  - `UpdateCentroidInPlace(float[] centroid, float[] nextVector, int countBefore)`: Update running average centroid
- **Statistics:**
  - `CalculateMean(double[] values)`: Calculate arithmetic mean
  - `CalculateStandardDeviation(double[] values, bool sample = false)`: Calculate standard deviation
  - `MinMaxNormalization(double[] values, double min, double max)`: Normalize values to 0-100 range
- **Utilities:**
  - `RoundToInt(double value)`: Round to nearest integer
  - `Clamp(double value, double min, double max)`: Clamp value to range
  - `Clamp(float value, float min, float max)`: Clamp float value to range

**WithSimilarity<T>** (where T : IVectorObject)
- `Item`: The wrapped item of type T
- `Similarity`: The similarity score (double)
- `CreateBetween(T theItem, float[] vectorA, float[] vectorB)`: Create from two vectors
- `CreateBetween(IVectorObject obj, float[] vector)`: Create from vector object and vector
- `CreateBetween(IVectorObject objA, IVectorObject objB)`: Create from two vector objects
- `CreateList(IEnumerable<T> items, float[] vector, bool onlyIncludeItemsWithValidVectors = true)`: Create list for collection
- Wraps any IVectorObject with a similarity score for ranking and filtering

**RelevanceRatedEntry<T>**
- `Item`: The item being rated for relevance (type T)
- `Relevance`: KnowledgebaseRelevanceMetrics for the item
- Represents a search result entry with comprehensive relevance scoring

**KnowledgebaseRelevanceMetrics**
- `CosineSimilarity`: Raw cosine similarity value (e.g., 0.82)
- `RelevanceScore`: Relevance score as integer (e.g., 82 for 0.82 similarity)
- `NormalizedScore`: Normalized score on 0-100 scale based on result set
- `StandardDeviation`: Standard deviation of top-k similarity scores
- Provides comprehensive metrics for search result relevance analysis

### Utility Classes

**StreamHashHelper** (Static Class)
- `GenerateSha256Hash(Stream stream)`: Generates SHA256 hash from stream content for file integrity verification
- Used for creating content hashes when storing knowledge files
- Note: Stream position will be at the end after hashing

### Interfaces

**ITokenizer**  
- `CountTokens(string text)`: Count tokens in text

**IVectorObject**
- `Vector()`: Returns the vector representation
- `ContainsVector()`: Returns true if vector is available

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
└── IKnowledgeVectorStore (Vector Embeddings)
```

### Quick Start with Storage

The library provides several ready-to-use storage implementations:

#### Using SQLite Storage (Recommended for most use cases)
```csharp
// Set up services using provided implementations
BertTokenizer tokenizer = await BertTokenizer.CreateAsync();
EasyReasyOllamaEmbeddingService embeddingService = await EasyReasyOllamaEmbeddingService.CreateAsync(
    baseUrl: "https://your-ollama-server.com",
    apiKey: "your-api-key",
    modelName: "nomic-embed-text");

// Create SQLite-based storage (requires EasyReasy.KnowledgeBase.Storage.Sqlite package)
SqliteKnowledgeStore knowledgeStore = await SqliteKnowledgeStore.CreateAsync("knowledge.db");

// Create vector storage using EasyReasy.VectorStorage
CosineVectorStore cosineVectorStore = new CosineVectorStore(embeddingService.Dimensions);
EasyReasyVectorStore vectorStore = new EasyReasyVectorStore(cosineVectorStore);

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
```

#### Using Custom Storage Implementations
```csharp
// You can also create your own implementations of the storage interfaces
IFileStore fileStore = new YourCustomFileStore();
IChunkStore chunkStore = new YourCustomChunkStore();
ISectionStore sectionStore = new YourCustomSectionStore();
IKnowledgeVectorStore vectorStore = new YourCustomVectorStore();

// Create the main knowledge store
KnowledgeStore knowledgeStore = new KnowledgeStore(fileStore, chunkStore, sectionStore);
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

// Get multiple chunks by IDs
IEnumerable<KnowledgeFileChunk> chunks = await chunkStore.GetAsync(chunkIds);

// Get chunk by index within section
KnowledgeFileChunk? chunk = await chunkStore.GetByIndexAsync(sectionId, chunkIndex);

// Get all chunks for a section
IEnumerable<KnowledgeFileChunk> sectionChunks = await chunkStore.GetBySectionAsync(sectionId);

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

// Delete all sections for a file
bool deleted = await sectionStore.DeleteByFileAsync(fileId);
```

#### IKnowledgeVectorStore
Manages vector embeddings for similarity search:

```csharp
// Add a vector
await vectorStore.AddAsync(entityId, embedding);

// Remove a vector
await vectorStore.RemoveAsync(entityId);

// Search for similar vectors
IEnumerable<IKnowledgeVector> similarVectors = await vectorStore.SearchAsync(queryVector, maxResults);
```

#### IExplicitPersistence
Defines explicit persistence operations for storage components that need manual control over data loading and saving:

```csharp
// Load data from persistent storage during startup
await storageComponent.LoadAsync(cancellationToken);

// Save data to persistent storage during shutdown
await storageComponent.SaveAsync(cancellationToken);
```

### Storage Implementations

**KnowledgeStore** : IKnowledgeStore
- Constructor: `(IFileStore files, IChunkStore chunks, ISectionStore sections)`
- `Files`: IFileStore - Access to file storage
- `Chunks`: IChunkStore - Access to chunk storage
- `Sections`: ISectionStore - Access to section storage
- Provides unified interface for managing knowledge files, chunks, and sections
- Sealed class for consistent storage access patterns

**SqliteKnowledgeStore** : IKnowledgeStore, IExplicitPersistence
- Constructor: `(string connectionString)` - Creates with SQLite connection string
- `CreateAsync(string path, CancellationToken cancellationToken)`: Static factory method for easy setup
- `LoadAsync(CancellationToken cancellationToken)`: Loads and initializes database schema
- `SaveAsync(CancellationToken cancellationToken)`: Saves data (no-op for SQLite as it's transactional)
- Complete SQLite-based implementation with automatic schema creation
- Available in `EasyReasy.KnowledgeBase.Storage.Sqlite` package

**EasyReasyVectorStore** : IKnowledgeVectorStore
- Constructor: `(IVectorStore vectorStore)` - Wraps any IVectorStore implementation
- Integrates with EasyReasy.VectorStorage for high-performance vector operations
- Available in `EasyReasy.KnowledgeBase.Storage.IntegratedVectorStore` package

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
    
    public float[] Vector() => Embedding ?? Array.Empty<float>();
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
    public string? AdditionalContext { get; set; }
    public List<KnowledgeFileChunk> Chunks { get; set; }
    public float[]? Embedding { get; set; }
    
    public static KnowledgeFileSection CreateFromChunks(List<KnowledgeFileChunk> chunks, Guid fileId, int sectionIndex);
    public float[] Vector() => Embedding ?? Array.Empty<float>();
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

#### Vector Store Implementation
For vector store implementations, see [EasyReasy.KnowledgeBase.Storage.IntegratedVectorStore](../EasyReasy.KnowledgeBase.Storage.IntegratedVectorStore) which provides a complete vector storage solution.

## Dependencies

- **.NET 8.0+**: Modern async/await patterns and performance features