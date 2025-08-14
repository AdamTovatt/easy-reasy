# EasyReasy.KnowledgeBase.OllamaGeneration

[← Back to EasyReasy System](../README.md)

[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.KnowledgeBase.OllamaGeneration-blue.svg)](https://www.nuget.org/packages/EasyReasy.KnowledgeBase.OllamaGeneration)

Ollama-based implementations for EasyReasy.KnowledgeBase generation services. Provides embedding generation and one-shot text processing using the EasyReasy.Ollama.Client library.

## Features

- **🧠 Embedding Service**: Generate embeddings from text using Ollama models
- **🎯 One-Shot Processing**: AI-powered text processing (summarization, question generation, etc.)
- **🔐 Automatic Authentication**: Built-in JWT authentication with API key
- **⚡ Async/Await**: Full async support with cancellation tokens
- **🛡️ Error Handling**: Comprehensive error handling and resource disposal

## Quick Start

### Installation

```bash
dotnet add package EasyReasy.KnowledgeBase.OllamaGeneration
```

### Embedding Service

```csharp
using EasyReasy.KnowledgeBase.OllamaGeneration;

// Create embedding service
EasyReasyOllamaEmbeddingService embeddingService = await EasyReasyOllamaEmbeddingService.CreateAsync(
    baseUrl: "https://your-ollama-server.com",
    apiKey: "your-api-key", 
    modelName: "nomic-embed-text");

// Generate embeddings
float[] embeddings = await embeddingService.EmbedAsync("Hello, world!");

// Dispose when done
embeddingService.Dispose();
```

### One-Shot Service

```csharp
using EasyReasy.KnowledgeBase.OllamaGeneration;

// Create one-shot service  
EasyReasyOllamaOneShotService oneShotService = await EasyReasyOllamaOneShotService.CreateAsync(
    baseUrl: "https://your-ollama-server.com",
    apiKey: "your-api-key",
    modelName: "llama3.1");

// Process text with AI
string systemPrompt = "Summarize the following text in 2-3 sentences:";
string userInput = "Long document text here...";
string summary = await oneShotService.ProcessAsync(systemPrompt, userInput);

// Dispose when done
oneShotService.Dispose();
```

### Using with KnowledgeBase

```csharp
// Use as embedding service for document processing
SectionReaderFactory factory = new SectionReaderFactory(embeddingService, tokenizer);
using Stream stream = File.OpenRead("document.md");
SectionReader reader = factory.CreateForMarkdown(stream, maxTokensPerChunk: 100, maxTokensPerSection: 1000);

await foreach (List<KnowledgeFileChunk> chunks in reader.ReadSectionsAsync())
{
    // Process sections...
}
```

## API Reference

### EasyReasyOllamaEmbeddingService

**Creation**
```csharp
static Task<EasyReasyOllamaEmbeddingService> CreateAsync(
    string baseUrl, 
    string apiKey, 
    string modelName, 
    CancellationToken cancellationToken = default)
```

**Methods**
- `EmbedAsync(string text, CancellationToken cancellationToken = default)`: Generate embedding vector
- `Dispose()`: Clean up resources

### EasyReasyOllamaOneShotService  

**Creation**
```csharp
static Task<EasyReasyOllamaOneShotService> CreateAsync(
    string baseUrl,
    string apiKey, 
    string modelName,
    CancellationToken cancellationToken = default)
```

**Methods**
- `ProcessAsync(string systemPrompt, string userInput, CancellationToken cancellationToken = default)`: Process text with AI
- `Dispose()`: Clean up resources

## Dependencies

- **.NET 8.0+**: Modern async/await patterns
- **EasyReasy.KnowledgeBase**: Core interfaces (`IEmbeddingService`, `IOneShotService`)  
- **EasyReasy.Ollama.Client**: Ollama API client library
- **EasyReasy.Ollama.Common**: Shared models and types

## License

MIT