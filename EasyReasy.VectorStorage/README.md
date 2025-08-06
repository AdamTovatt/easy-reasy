# EasyReasy.VectorStorage

[← Back to EasyReasy System](../README.md)

[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.VectorStorage-blue.svg)](https://www.nuget.org/packages/EasyReasy.VectorStorage)

A high-performance .NET library for vector similarity search similarity with optimized parallel processing and efficient memory management.

## Overview

EasyReasy.VectorStorage provides a fast and memory-efficient solution for storing and searching high-dimensional vectors using cosine similarity. It's designed for applications that need to find similar vectors quickly, such as recommendation systems, semantic search, and machine learning applications.

**Disclaimer:**
If you're building a system that needs to search among millions of vectors you should probably use an external, dedicated vector database. But if you're building a system that will is will be storing less than 100 000 vectors per vector store this library is great. For 10 000 vectors, the average search time for finding the top 10 closest results is around 13 ms on a quite old laptop (4 cores, Intel i7-10510U @ 1.8GHz). Memory usage for those 10 000 vectors is 30 MB.

**Why Use EasyReasy.VectorStorage?**

- **High Performance**: Optimized cosine similarity calculations with SIMD support and parallel processing
- **Memory Efficient**: Optimized MinHeap implementation with reduced allocations and better cache locality
- **Thread Safe**: Built-in thread safety with ReaderWriterLockSlim for concurrent access
- **Persistence**: Save and load vector stores to/from streams
- **Flexible Dimensions**: Support for any vector dimension with optimized paths for common sizes (768, 1024, etc.)
- **Automatic Parallelization**: Automatically switches to parallel processing for large datasets (>1000 vectors)

## Quick Start

```csharp
// Create a vector store to store vectors
// Here 768 is used as dimension but any dimension can be used
IVectorStore store = new CosineVectorStore(768);

// Add vectors
await store.AddAsync(new StoredVector(Guid.NewGuid(), embeddingVector));

// Find similar vectors
IEnumerable<StoredVector> similarVectors = await store.FindMostSimilarAsync(queryVector, count: 10);
```

## Core Concepts

### StoredVector
A lightweight struct that represents a vector with an ID and float values.
Here, public fields are exposed even though normally only public properties would be exposed. This reduced flexibility for future changes was chosen with the thought that a `StoredVector` is such a simple type and will probably never require future changes anyway:

```csharp
public readonly struct StoredVector
{
    public readonly Guid Id;
    public readonly float[] Values;
    
    public StoredVector(Guid id, float[] values);
    public ReadOnlySpan<float> GetSpan(); // High-performance access
}
```

### IVectorStore
The main interface for vector storage operations:

```csharp
public interface IVectorStore
{
    Task AddAsync(StoredVector vector);
    Task<bool> RemoveAsync(Guid id);
    Task<IEnumerable<StoredVector>> FindMostSimilarAsync(float[] queryVector, int count);
    Task SaveAsync(Stream stream);
    Task LoadAsync(Stream stream);
}
```

### CosineVectorStore
The primary implementation that provides high-performance cosine similarity search:

```csharp
public class CosineVectorStore : IVectorStore
{
    public CosineVectorStore(int dimension);
    // Implements all IVectorStore methods
}
```

## Getting Started

### 1. Create a Vector Store

```csharp
// 768 can be replaced with any other desired dimension
CosineVectorStore store = new CosineVectorStore(768);
```

### 2. Add Vectors

```csharp
// Create a vector
float[] embedding = CreateAnEmbeddingSomehow(somethingToEmbedd);

StoredVector vector = new StoredVector(Guid.NewGuid(), embedding);
await store.AddAsync(vector);
```

> [!TIP]
> The Guid.NewGuid() in this example is just to show that the constructor for `StoredVector` takes a `Guid`. Any `Guid` can be used, it doesn't have to be created as a new one here.

### 3. Find Similar Vectors

```csharp
// Create a query vector
float[] queryVector = CreateAnEmbeddingSomehow(somethingToSearchFor);

// Find the 10 most similar vectors
IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(queryVector, count: 10);
```

### 4. Remove Vectors

```csharp
Guid vectorId = GetGuidThatShouldBeRemovedSomehow();
bool removed = await store.RemoveAsync(vectorId);
```

### 5. Persistence

```csharp
// Save to file
using (FileStream saveStream = File.Create("vectors.dat"))
{
    await store.SaveAsync(saveStream);
}

// Load from file
using (FileStream loadStream = File.OpenRead("vectors.dat"))
{
    await store.LoadAsync(loadStream);
}
```

## Performance Features

### Automatic Parallel Processing

The library automatically switches between sequential and parallel processing based on dataset size:

- **Sequential**: For datasets with ≤1000 vectors
- **Parallel**: For datasets with >1000 vectors

This ensures optimal performance for both small and large datasets.

### Optimized Similarity Calculations

- **SIMD Support**: Uses hardware-accelerated vector operations when available
- **Optimized Paths**: Specialized implementations for common dimensions (768, 1024, etc.)
- **Memory Efficiency**: Uses `ReadOnlySpan<float>` for zero-copy operations
- **Cache Locality**: Optimized data structures for better CPU cache performance

### Efficient MinHeap Implementation

The internal MinHeap is optimized for top-K selection:

- **Reduced Allocations**: Separate arrays instead of tuples
- **Aggressive Inlining**: MethodImpl optimizations for critical paths
- **Memory Layout**: Optimized for cache locality
- **Zero-Copy Returns**: Uses `ReadOnlySpan<T>` for results

## Advanced Usage

### Working with Different Dimensions

The library supports any vector dimension. Simply specify the dimension in the constructor:

```csharp
CosineVectorStore store = new CosineVectorStore(1337); // All dimensions are supported

CosineVectorStore store = new CosineVectorStore(420); // Just pass whatever dimension you want to use to the constructor
```

**Optimized Dimensions**: The library has extra SIMD optimizations for 768 and 1024 dimensions, which are common in modern embedding models.

### Concurrent Access

The store is thread-safe and supports concurrent operations:

```csharp
// Multiple threads can add vectors simultaneously
Parallel.For(0, 1000, async i =>
{
    float[] vector = GenerateVector(768);
    await store.AddAsync(new StoredVector(Guid.NewGuid(), vector));
});

// Multiple threads can search simultaneously
Parallel.For(0, 10, async i =>
{
    float[] query = GenerateVector(768);
    var results = await store.FindMostSimilarAsync(query, 5);
});
```

## Error Handling

The library provides clear error messages for common issues:

```csharp
// Dimension mismatch when adding vectors
try
{
    await store.AddAsync(new StoredVector(Guid.NewGuid(), new float[512])); // Wrong dimension
}
catch (ArgumentException ex)
{
    // "Vector must have 768 dimensions."
}

// Dimension mismatch when searching
try
{
    await store.FindMostSimilarAsync(new float[512], 10); // Wrong dimension
}
catch (ArgumentException ex)
{
    // "Query vector must have 768 dimensions."
}

// Invalid parameters
try
{
    await store.FindMostSimilarAsync(null, 10); // Null query vector
}
catch (ArgumentException ex)
{
    // Returns empty collection for null/empty query vectors
}
```


## Performance Characteristics

### Memory Usage

- **Per Vector**: ~4 bytes per dimension + 16 bytes for GUID
- **768-dimensional vector**: ~3KB per vector
- **1000 vectors**: ~3MB total
- **10,000 vectors**: ~30MB total
- **100,000 vectors**: ~300MB total

> The memory usage values here are the theoretical values that are then also confirmed to be true in the memory performance tests.

### Search Performance

- **Time Complexity**: O(n) where n is the number of vectors in the store
- **Desktop (Intel i9-9900K @ 4.5GHz)**: ~13ms average for top 10 results in 100,000 vectors
- **Laptop (Intel i7-10510U @ 1.8GHz)**: ~22ms average for top 10 results in 10,000 vectors
- **Algorithm**: O(n) with optimized cosine similarity calculations
- **Parallel processing**: Automatically enabled for datasets >1000 vectors

### Persistence Performance

- **Laptop (Intel i7-10510U @ 1.8GHz)**: ~130ms to save 10,000 vectors (~80,000 vectors/second)
- **Laptop (Intel i7-10510U @ 1.8GHz)**: ~146ms to load 10,000 vectors (~70,000 vectors/second)

### Scalability

- **Small datasets**: Excellent performance with sequential processing
- **Large datasets**: Good performance with automatic parallelization
- **Very large datasets**: Consider sharding or specialized vector databases

## Dependencies

- **.NET 8.0+**: Modern .NET features and performance optimizations
- **System.Numerics**: For SIMD vector operations
- **System.Collections.Concurrent**: For parallel processing

## License
MIT