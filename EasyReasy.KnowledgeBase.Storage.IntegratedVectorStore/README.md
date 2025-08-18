# EasyReasy.KnowledgeBase.Storage.IntegratedVectorStore

[← Back to EasyReasy System](../README.md)

[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.KnowledgeBase.Storage.IntegratedVectorStore-blue.svg)](https://www.nuget.org/packages/EasyReasy.KnowledgeBase.Storage.IntegratedVectorStore)

A seamless integration layer that bridges EasyReasy.VectorStorage with the EasyReasy KnowledgeBase system, providing high-performance vector similarity search capabilities for knowledge management applications.

## Overview

EasyReasy.KnowledgeBase.Storage.IntegratedVectorStore provides a lightweight adapter that implements the `IKnowledgeVectorStore` interface using any `IVectorStore` implementation from the EasyReasy.VectorStorage library. This enables you to leverage the high-performance vector storage capabilities of EasyReasy.VectorStorage within the KnowledgeBase ecosystem.

**Why Use EasyReasy.KnowledgeBase.Storage.IntegratedVectorStore?**

- **Seamless Integration**: Bridges EasyReasy.VectorStorage with KnowledgeBase interfaces
- **High Performance**: Leverages the optimized vector operations from EasyReasy.VectorStorage
- **Flexible Backend**: Works with any IVectorStore implementation (CosineVectorStore, custom implementations, etc.)
- **Zero Overhead**: Minimal adapter layer with no performance impact
- **Type Safety**: Provides strongly-typed integration between vector storage and knowledge base
- **Future-Proof**: Automatically benefits from improvements in EasyReasy.VectorStorage

## Quick Start

```csharp
// Create a vector store from EasyReasy.VectorStorage
CosineVectorStore cosineVectorStore = new CosineVectorStore(768);

// Wrap it with the integrated vector store
EasyReasyVectorStore knowledgeVectorStore = new EasyReasyVectorStore(cosineVectorStore);

// Use with KnowledgeBase
ISearchableKnowledgeStore searchableStore = new SearchableKnowledgeStore(knowledgeStore, knowledgeVectorStore);

// Add vectors
await knowledgeVectorStore.AddAsync(chunkId, embeddingVector);

// Search for similar vectors
IEnumerable<IKnowledgeVector> similarVectors = await knowledgeVectorStore.SearchAsync(queryVector, maxResults: 10);
```

## Core Concepts

### EasyReasyVectorStore
The main adapter class that implements `IKnowledgeVectorStore` using an underlying `IVectorStore`:

```csharp
public class EasyReasyVectorStore : IKnowledgeVectorStore
{
    public EasyReasyVectorStore(IVectorStore vectorStore);
    
    // IKnowledgeVectorStore implementation
    public Task AddAsync(Guid guid, float[] vector);
    public Task RemoveAsync(Guid guid);
    public Task<IEnumerable<IKnowledgeVector>> SearchAsync(float[] queryVector, int maxResultsCount);
}
```

### Integration Architecture
The adapter provides a clean separation between vector storage and knowledge base concerns:

```
KnowledgeBase System
├── IKnowledgeVectorStore (Interface)
├── EasyReasyVectorStore (Adapter)
└── IVectorStore (EasyReasy.VectorStorage)
    ├── CosineVectorStore
    ├── CustomVectorStore
    └── Other implementations...
```

## Getting Started

### 1. Create a Vector Store

```csharp
// Create the underlying vector store
CosineVectorStore cosineVectorStore = new CosineVectorStore(768);

// Wrap with the integrated vector store
EasyReasyVectorStore knowledgeVectorStore = new EasyReasyVectorStore(cosineVectorStore);
```

### 2. Integrate with KnowledgeBase

```csharp
// Create knowledge store (e.g., SQLite-based)
SqliteKnowledgeStore knowledgeStore = await SqliteKnowledgeStore.CreateAsync("knowledge.db");

// Create searchable knowledge store with vector capabilities
ISearchableKnowledgeStore searchableStore = new SearchableKnowledgeStore(
    knowledgeStore, 
    knowledgeVectorStore);
```

### 3. Add Vectors

```csharp
// Add vectors for knowledge entities
foreach (KnowledgeFileChunk chunk in chunks)
{
    if (chunk.Embedding != null)
    {
        await knowledgeVectorStore.AddAsync(chunk.Id, chunk.Embedding);
    }
}
```

### 4. Search for Similar Vectors

```csharp
// Search for similar content
float[] queryVector = await embeddingService.EmbedAsync("your search query");
IEnumerable<IKnowledgeVector> similarVectors = await knowledgeVectorStore.SearchAsync(queryVector, maxResults: 10);

// Process results
foreach (IKnowledgeVector vector in similarVectors)
{
    Console.WriteLine($"Similar vector ID: {vector.Id}");
    Console.WriteLine($"Vector dimensions: {vector.Vector().Length}");
}
```

### 5. Remove Vectors

```csharp
// Remove vectors when content is deleted
await knowledgeVectorStore.RemoveAsync(chunkId);
```

## Advanced Usage

### Custom Vector Store Backends

The adapter works with any `IVectorStore` implementation:

```csharp
// Use with custom vector store implementations
IVectorStore customVectorStore = new YourCustomVectorStore();
EasyReasyVectorStore knowledgeVectorStore = new EasyReasyVectorStore(customVectorStore);

// Or with different EasyReasy.VectorStorage implementations
CosineVectorStore cosineStore = new CosineVectorStore(1024);
EasyReasyVectorStore knowledgeVectorStore = new EasyReasyVectorStore(cosineStore);
```

### Complete KnowledgeBase Setup

```csharp
// Set up services
BertTokenizer tokenizer = await BertTokenizer.CreateAsync();
EasyReasyOllamaEmbeddingService embeddingService = await EasyReasyOllamaEmbeddingService.CreateAsync(
    baseUrl: "https://your-ollama-server.com",
    apiKey: "your-api-key",
    modelName: "nomic-embed-text");

// Create storage components
SqliteKnowledgeStore knowledgeStore = await SqliteKnowledgeStore.CreateAsync("knowledge.db");
CosineVectorStore cosineVectorStore = new CosineVectorStore(embeddingService.Dimensions);
EasyReasyVectorStore vectorStore = new EasyReasyVectorStore(cosineVectorStore);

// Create searchable knowledge store
ISearchableKnowledgeStore searchableStore = new SearchableKnowledgeStore(knowledgeStore, vectorStore);

// Create knowledge base
ISearchableKnowledgeBase knowledgeBase = new SearchableKnowledgeBase(
    searchableStore, 
    embeddingService, 
    tokenizer);
```

### Persistence Integration

The adapter automatically works with the persistence capabilities of the underlying vector store:

```csharp
// Save vector store data
using (FileStream saveStream = File.OpenWrite("vectors.dat"))
{
    await cosineVectorStore.SaveAsync(saveStream);
}

// Load vector store data
using (FileStream loadStream = File.OpenRead("vectors.dat"))
{
    await cosineVectorStore.LoadAsync(loadStream);
}

// The EasyReasyVectorStore automatically uses the persisted data
```

## Performance Characteristics

### Zero Overhead Adapter
The `EasyReasyVectorStore` adapter adds minimal overhead:

- **Memory**: ~16 bytes per instance (single reference to underlying store)
- **CPU**: Negligible overhead for method calls
- **Latency**: Direct delegation to underlying vector store

### Vector Operations Performance
Performance characteristics depend on the underlying `IVectorStore` implementation:

- **CosineVectorStore**: O(n) search time with SIMD optimizations
- **Custom implementations**: Performance varies by implementation
- **Memory usage**: Determined by underlying vector store

### Scalability
The adapter scales with the underlying vector store:

- **Small datasets**: Excellent performance with any backend
- **Large datasets**: Performance depends on vector store implementation
- **Concurrent access**: Inherits thread safety from underlying store

### Error Handling

The adapter provides transparent error handling:

```csharp
try
{
    await knowledgeVectorStore.AddAsync(chunkId, embedding);
}
catch (ArgumentException ex)
{
    // Handle dimension mismatch or invalid parameters
    // Errors are propagated from the underlying vector store
}

try
{
    var results = await knowledgeVectorStore.SearchAsync(queryVector, maxResults);
}
catch (InvalidOperationException ex)
{
    // Handle search errors from underlying vector store
}
```

## Dependencies

- **.NET 8.0+**: Modern async/await patterns and performance features
- **EasyReasy.KnowledgeBase**: Core KnowledgeBase interfaces and models
- **EasyReasy.VectorStorage**: High-performance vector storage library

## Migration and Compatibility

### Upgrading Vector Storage
The adapter automatically benefits from improvements in EasyReasy.VectorStorage:

```csharp
// Update EasyReasy.VectorStorage package
// No changes needed to EasyReasyVectorStore usage
// Automatically gets performance improvements and new features
```

### Backward Compatibility
The adapter maintains compatibility with existing vector store implementations:

```csharp
// Existing code continues to work
EasyReasyVectorStore vectorStore = new EasyReasyVectorStore(existingVectorStore);

// New features are available when underlying store supports them
```

## Troubleshooting

### Common Issues

**Dimension Mismatch**
- Ensure the underlying vector store dimension matches your embeddings
- Check that all vectors have the same dimension

**Performance Issues**
- Verify the underlying vector store is properly configured
- Consider using CosineVectorStore for optimal performance
- Check vector store persistence settings

**Integration Issues**
- Ensure both EasyReasy.KnowledgeBase and EasyReasy.VectorStorage are referenced
- Verify vector store initialization before creating the adapter

### Debugging

```csharp
// Check underlying vector store state
if (cosineVectorStore is IExplicitPersistence persistence)
{
    await persistence.LoadAsync();
}

// Verify vector store dimensions
Console.WriteLine($"Vector store dimensions: {cosineVectorStore.Dimension}");

// Test vector operations directly
await cosineVectorStore.AddAsync(new StoredVector(Guid.NewGuid(), testVector));
```

## License
MIT
