# EasyReasy.KnowledgeBase.Storage.Sqlite

[← Back to EasyReasy System](../README.md)

[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.KnowledgeBase.Storage.Sqlite-blue.svg)](https://www.nuget.org/packages/EasyReasy.KnowledgeBase.Storage.Sqlite)

A SQLite-based storage implementation for the EasyReasy KnowledgeBase system, providing persistent storage for knowledge files, sections, and chunks with automatic schema management and transaction support.

## Overview

EasyReasy.KnowledgeBase.Storage.Sqlite provides a complete SQLite implementation of the KnowledgeBase storage interfaces, offering reliable, file-based persistence for knowledge management applications. It's designed for applications that need local, embedded database storage with full ACID compliance and automatic schema management.

**Why Use EasyReasy.KnowledgeBase.Storage.Sqlite?**

- **Complete Implementation**: Full implementation of all KnowledgeBase storage interfaces
- **Automatic Schema Management**: Creates and manages database tables automatically
- **ACID Compliance**: Full transaction support with SQLite's reliability
- **File-Based**: Single database file for easy backup and deployment
- **Zero Configuration**: Works out of the box with minimal setup
- **Performance Optimized**: Includes database indexes for efficient queries
- **Cross-Platform**: Works on Windows, macOS, and Linux

## Quick Start

```csharp
// Create a SQLite knowledge store
SqliteKnowledgeStore knowledgeStore = await SqliteKnowledgeStore.CreateAsync("knowledge.db");

// Add a knowledge file
KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "document.md", contentHash);
Guid fileId = await knowledgeStore.Files.AddAsync(file);

// Add chunks with embeddings
foreach (KnowledgeFileChunk chunk in chunks)
{
    await knowledgeStore.Chunks.AddAsync(chunk);
}

// Add sections
foreach (KnowledgeFileSection section in sections)
{
    await knowledgeStore.Sections.AddAsync(section);
}
```

## Core Concepts

### SqliteKnowledgeStore
The main class that provides access to all SQLite storage components:

```csharp
public class SqliteKnowledgeStore : IKnowledgeStore, IExplicitPersistence
{
    public IFileStore Files { get; }
    public IChunkStore Chunks { get; }
    public ISectionStore Sections { get; }
    
    public SqliteKnowledgeStore(string connectionString);
    public static Task<SqliteKnowledgeStore> CreateAsync(string path, CancellationToken cancellationToken = default);
    public Task LoadAsync(CancellationToken cancellationToken = default);
    public Task SaveAsync(CancellationToken cancellationToken = default);
}
```

### Storage Components
The SQLite implementation provides three main storage components:

- **SqliteFileStore**: Manages knowledge file metadata
- **SqliteChunkStore**: Stores individual content chunks with embeddings
- **SqliteSectionStore**: Manages sections containing multiple chunks

## Getting Started

### 1. Create a Knowledge Store

```csharp
// Simple creation with file path
SqliteKnowledgeStore knowledgeStore = await SqliteKnowledgeStore.CreateAsync("knowledge.db");

// Or with custom connection string
string connectionString = "Data Source=knowledge.db;Mode=ReadWriteCreate";
SqliteKnowledgeStore knowledgeStore = new SqliteKnowledgeStore(connectionString);
await knowledgeStore.LoadAsync();
```

### 2. Store Knowledge Files

```csharp
// Create a knowledge file
byte[] contentHash = ComputeContentHash(fileContent);
KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "document.md", contentHash);

// Add to storage
Guid fileId = await knowledgeStore.Files.AddAsync(file);
```

### 3. Store Content Chunks

```csharp
// Create chunks with embeddings
foreach (var chunkData in processedChunks)
{
    KnowledgeFileChunk chunk = new KnowledgeFileChunk(
        id: Guid.NewGuid(),
        sectionId: sectionId,
        chunkIndex: chunkIndex,
        content: chunkData.Content,
        embedding: chunkData.Embedding);
    
    await knowledgeStore.Chunks.AddAsync(chunk);
}
```

### 4. Store Sections

```csharp
// Create sections from chunks
KnowledgeFileSection section = KnowledgeFileSection.CreateFromChunks(
    chunks: chunkList,
    fileId: fileId,
    sectionIndex: sectionIndex);

await knowledgeStore.Sections.AddAsync(section);
```

### 5. Retrieve Data

```csharp
// Get a file
KnowledgeFile? file = await knowledgeStore.Files.GetAsync(fileId);

// Get chunks for a section
IEnumerable<KnowledgeFileChunk> chunks = await knowledgeStore.Chunks.GetBySectionAsync(sectionId);

// Get a section
KnowledgeFileSection? section = await knowledgeStore.Sections.GetAsync(sectionId);
```

## Database Schema

The SQLite implementation automatically creates the following schema:

### knowledge_files Table
```sql
CREATE TABLE knowledge_files (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    hash BLOB NOT NULL
);
```

### knowledge_sections Table
```sql
CREATE TABLE knowledge_sections (
    id TEXT PRIMARY KEY,
    file_id TEXT NOT NULL,
    section_index INTEGER NOT NULL,
    summary TEXT,
    additional_context TEXT,
    embedding BLOB,
    FOREIGN KEY (file_id) REFERENCES knowledge_files (id) ON DELETE CASCADE
);
```

### knowledge_chunks Table
```sql
CREATE TABLE knowledge_chunks (
    id TEXT PRIMARY KEY,
    section_id TEXT NOT NULL,
    chunk_index INTEGER NOT NULL,
    content TEXT NOT NULL,
    embedding BLOB,
    file_id TEXT NOT NULL,
    FOREIGN KEY (section_id) REFERENCES knowledge_sections (id) ON DELETE CASCADE
);
```

### Indexes
The implementation automatically creates indexes for optimal performance:

```sql
-- Section indexes
CREATE INDEX idx_sections_file_id ON knowledge_sections (file_id);
CREATE INDEX idx_sections_file_index ON knowledge_sections (file_id, section_index);

-- Chunk indexes
CREATE INDEX idx_chunks_section_id ON knowledge_chunks (section_id);
CREATE INDEX idx_chunks_file_id ON knowledge_chunks (file_id);
CREATE INDEX idx_chunks_section_index ON knowledge_chunks (section_id, chunk_index);
```

## Advanced Usage

### Custom Connection Strings

```csharp
// In-memory database
string connectionString = "Data Source=:memory:";
SqliteKnowledgeStore memoryStore = new SqliteKnowledgeStore(connectionString);

// Read-only database
string connectionString = "Data Source=knowledge.db;Mode=ReadOnly";
SqliteKnowledgeStore readOnlyStore = new SqliteKnowledgeStore(connectionString);

// With custom settings
string connectionString = "Data Source=knowledge.db;Mode=ReadWriteCreate;Cache=Shared;Journal Mode=WAL";
SqliteKnowledgeStore customStore = new SqliteKnowledgeStore(connectionString);
```

### Explicit Persistence Control

```csharp
// Load data during startup
await knowledgeStore.LoadAsync(cancellationToken);

// Save data during shutdown (no-op for SQLite as it's transactional)
await knowledgeStore.SaveAsync(cancellationToken);
```

### Transaction Support

SQLite provides automatic transaction support for all operations:

```csharp
// All operations are automatically wrapped in transactions
await knowledgeStore.Files.AddAsync(file);
await knowledgeStore.Chunks.AddAsync(chunk);
await knowledgeStore.Sections.AddAsync(section);

// If any operation fails, all changes are rolled back
```

### Error Handling

The implementation provides clear error handling for common scenarios:

```csharp
try
{
    await knowledgeStore.Files.AddAsync(file);
}
catch (SqliteException ex)
{
    // Handle database-specific errors
    // e.g., constraint violations, connection issues
}

try
{
    await knowledgeStore.LoadAsync();
}
catch (ArgumentException ex)
{
    // Handle invalid connection string
}
```

## Performance Characteristics

### Storage Efficiency

- **File Metadata**: ~100 bytes per file
- **Chunk Storage**: ~1KB per chunk (varies with content length)
- **Section Storage**: ~500 bytes per section + chunk references
- **Embeddings**: ~4KB per embedding (768 dimensions)

### Query Performance

- **File Lookup**: O(1) with primary key index
- **Section Lookup**: O(1) with primary key index
- **Chunk Lookup**: O(1) with primary key index
- **Section by File**: O(log n) with file_id index
- **Chunks by Section**: O(log n) with section_id index

### Scalability

- **Small datasets**: Excellent performance with SQLite's optimized engine
- **Medium datasets**: Good performance with automatic indexing
- **Large datasets**: Consider database optimization for very large collections

## Integration with KnowledgeBase

### Complete KnowledgeBase Setup

```csharp
// Set up services
BertTokenizer tokenizer = await BertTokenizer.CreateAsync();
EasyReasyOllamaEmbeddingService embeddingService = await EasyReasyOllamaEmbeddingService.CreateAsync(
    baseUrl: "https://your-ollama-server.com",
    apiKey: "your-api-key",
    modelName: "nomic-embed-text");

// Create SQLite storage
SqliteKnowledgeStore knowledgeStore = await SqliteKnowledgeStore.CreateAsync("knowledge.db");

// Create vector storage
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

### Indexing Documents

```csharp
// Create indexer
IIndexer indexer = knowledgeBase.CreateIndexer();

// Index documents from file sources
IFileSourceProvider fileSourceProvider = new YourFileSourceProvider();
foreach (IFileSource fileSource in await fileSourceProvider.GetAllFilesAsync())
{
    await indexer.ConsumeAsync(fileSource);
}
```

## Dependencies

- **.NET 8.0+**: Modern async/await patterns and performance features
- **Microsoft.Data.Sqlite**: Official Microsoft SQLite provider for .NET
- **EasyReasy.KnowledgeBase**: Core KnowledgeBase interfaces and models

## Migration and Backup

### Database Backup

```csharp
// Simple file copy backup
File.Copy("knowledge.db", "knowledge_backup.db");

// Or use SQLite's backup API for larger databases
using (var connection = new SqliteConnection("Data Source=knowledge.db"))
{
    connection.Open();
    connection.BackupDatabase("backup.db");
}
```

### Database Migration

The implementation automatically handles schema migrations by using `CREATE TABLE IF NOT EXISTS` statements. When new versions add features, the schema will be automatically updated.

## Troubleshooting

### Common Issues

**Database Locked**
- Ensure only one process accesses the database file
- Use WAL mode for better concurrency: `Journal Mode=WAL`

**Out of Memory**
- Large embeddings can consume significant memory
- Consider streaming for very large datasets

**Performance Issues**
- Ensure indexes are created (automatic)
- Consider database optimization for large datasets
- Use appropriate connection string settings

### Connection String Options

```csharp
// Recommended settings for production
string connectionString = "Data Source=knowledge.db;Mode=ReadWriteCreate;Cache=Shared;Journal Mode=WAL;Synchronous=Normal";

// Settings for development
string connectionString = "Data Source=knowledge.db;Mode=ReadWriteCreate;Cache=Private;Journal Mode=Delete";
```

## License
MIT
