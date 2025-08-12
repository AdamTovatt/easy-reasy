# EasyReasy.KnowledgeBase

[← Back to EasyReasy System](../README.md)

[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.KnowledgeBase-blue.svg)](https://www.nuget.org/packages/EasyReasy.KnowledgeBase)

A .NET library for processing and chunking knowledge documents with confidence rating capabilities.

## Quick Start

```csharp
// Create a chunking configuration
ChunkingConfiguration config = new ChunkingConfiguration
{
    MaxTokensPerChunk = 512,
    Tokenizer = new SimpleTokenizer() // or BertTokenizer from EasyReasy.KnowledgeBase.BertTokenization
};

// Create a text segment reader for markdown content
TextSegmentReader reader = new TextSegmentReader(markdownContent);

// Create a chunk reader
SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(reader, config);

// Read chunks
while (true)
{
    string? chunk = await chunkReader.ReadNextChunkContentAsync();
    if (chunk == null) break;
    // Process chunk...
}
```

## Core Components

### Chunking

**ChunkingConfiguration**
- `MaxTokensPerChunk`: Maximum tokens per chunk
- `Tokenizer`: ITokenizer implementation

**ITokenizer**
- `CountTokens(string text)`: Count tokens in text

**IKnowledgeChunkReader**
- `ReadNextChunkContentAsync()`: Returns next chunk as string or null

**SegmentBasedChunkReader**
- Constructor: `(TextSegmentReader, ChunkingConfiguration)`
- `ReadNextChunkContentAsync()`: Returns next chunk as string or null

**TextSegmentReader**
- Constructor: `(string content)`
- `ReadNextTextSegmentAsync()`: Returns next text segment or null

**ITextSegmentReader**
- `ReadNextTextSegmentAsync()`: Returns next text segment or null

### Confidence Rating

**ConfidenceMath** (Static Class)
- `DotProduct(float[] a, float[] b)`: Returns double
- `VectorNorm(float[] v)`: Returns double
- `NormalizeVector(float[] v)`: Returns float[]
- `NormalizeVectorInPlace(float[] v)`: void
- `CosineSimilarity(float[] a, float[] b)`: Returns float
- `CosineSimilarityPreNormalized(float[] a, float[] b)`: Returns float
- `UpdateCentroidInPlace(float[] centroid, float[] nextVector, int countBefore)`: void
- `CalculateMean(double[] values)`: Returns double
- `CalculateStandardDeviation(double[] values, bool sample = false)`: Returns double
- `MinMaxNormalization(double[] values, double min, double max)`: Returns double[]
- `RoundToInt(double value)`: Returns int
- `Clamp(double value, double min, double max)`: Returns double
- `Clamp(float value, float min, float max)`: Returns float

**IVectorObject**
- `GetVector()`: Returns float[]
- `ContainsVector()`: Returns bool

**KnowledgebaseRelevanceMetrics**
- `CosineSimilarity`: double
- `RelevanceScore`: int
- `NormalizedScore`: double
- `StandardDeviation`: double
- Constructor: `(double cosineSimilarity, int relevanceScore, double normalizedScore, double standardDeviation)`

**RelevanceRatedEntry<T>**
- `Item`: T
- `Relevance`: KnowledgebaseRelevanceMetrics
- Constructor: `(T item, KnowledgebaseRelevanceMetrics relevance)`

**WithSimilarity<T>** where T : IVectorObject
- `Item`: T
- `Similarity`: double
- Constructor: `(T item, double similarity)`
- `CreateBetween(T theItem, float[] vectorA, float[] vectorB)`: Returns WithSimilarity<T>
- `CreateBetween(IVectorObject obj, float[] vector)`: Returns WithSimilarity<T>
- `CreateBetween(IVectorObject objA, IVectorObject objB)`: Returns WithSimilarity<T>
- `CreateList(IEnumerable<T> items, float[] vector, bool onlyIncludeItemsWithValidVectors = true)`: Returns List<WithSimilarity<T>>

### Models

**KnowledgeFile**
- `Id`: Guid
- `Name`: string
- `Content`: string
- `Sections`: List<KnowledgeFileSection>

**KnowledgeFileSection**
- `Title`: string
- `Content`: string
- `Chunks`: List<KnowledgeFileChunk>

**KnowledgeFileChunk**
- `Id`: Guid
- `Content`: string
- `TokenCount`: int
- `SectionTitle`: string

## Usage Examples

### Basic Chunking
```csharp
ChunkingConfiguration config = new ChunkingConfiguration { MaxTokensPerChunk = 512, Tokenizer = new SimpleTokenizer() };
TextSegmentReader reader = new TextSegmentReader(markdownContent);
SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(reader, config);

while (true)
{
    string? chunk = await chunkReader.ReadNextChunkContentAsync();
    if (chunk == null) break;
    Console.WriteLine(chunk);
}
```

### Vector Similarity
```csharp
MyVectorObject item1 = new MyVectorObject(vector1);
MyVectorObject item2 = new MyVectorObject(vector2);

WithSimilarity<MyVectorObject> similarity = WithSimilarity<MyVectorObject>.CreateBetween(item1, item2);
Console.WriteLine($"Similarity: {similarity.Similarity}");
```

### Confidence Calculations
```csharp
float similarity = ConfidenceMath.CosineSimilarity(vectorA, vectorB);
float[] normalized = ConfidenceMath.NormalizeVector(vector);
double mean = ConfidenceMath.CalculateMean(values);
```

## Dependencies

- **.NET 8.0+**: Modern .NET features
- **EasyReasy.KnowledgeBase.BertTokenization** (optional): For BERT tokenization

## License
MIT 