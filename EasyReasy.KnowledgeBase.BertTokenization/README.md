# EasyReasy.KnowledgeBase.BertTokenization

[← Back to EasyReasy System](../README.md)

[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.KnowledgeBase.BertTokenization-blue.svg)](https://www.nuget.org/packages/EasyReasy.KnowledgeBase.BertTokenization)

BERT-based tokenizer implementation for EasyReasy.KnowledgeBase. Provides accurate token counting and text processing using the FastBertTokenizer library with BERT base uncased vocabulary.

## Features

- **🤖 BERT Tokenization**: Industry-standard BERT base uncased vocabulary
- **📊 Token Counting**: Accurate token count for chunking and size management
- **🔄 Encode/Decode**: Full tokenization and detokenization support
- **⚡ Async Creation**: Async initialization with Hugging Face model loading
- **🛡️ Truncation Control**: Configurable maximum token limits

## Quick Start

### Installation

```bash
dotnet add package EasyReasy.KnowledgeBase.BertTokenization
```

### Basic Usage

```csharp
using EasyReasy.KnowledgeBase.BertTokenization;

// Create tokenizer
BertTokenizer tokenizer = await BertTokenizer.CreateAsync();

// Count tokens
int tokenCount = tokenizer.CountTokens("Hello, world!");

// Encode text to tokens
int[] tokens = tokenizer.Encode("This is a test sentence.");

// Decode tokens back to text
string decoded = tokenizer.Decode(tokens);

Console.WriteLine($"Token count: {tokenCount}");
Console.WriteLine($"Tokens: [{string.Join(", ", tokens)}]");
Console.WriteLine($"Decoded: {decoded}");
```

### Using with KnowledgeBase

```csharp
using EasyReasy.KnowledgeBase.BertTokenization;
using EasyReasy.KnowledgeBase.Chunking;

// Create tokenizer for use with document processing
BertTokenizer tokenizer = await BertTokenizer.CreateAsync();

// Use with section reader factory
SectionReaderFactory factory = new SectionReaderFactory(embeddingService, tokenizer);
using Stream stream = File.OpenRead("document.md");
SectionReader reader = factory.CreateForMarkdown(stream, maxTokensPerChunk: 100, maxTokensPerSection: 1000);

await foreach (List<KnowledgeFileChunk> chunks in reader.ReadSectionsAsync())
{
    // Process sections with accurate token counts
}
```

### Custom Configuration

```csharp
// Configure maximum encoding tokens to prevent truncation
BertTokenizer tokenizer = await BertTokenizer.CreateAsync();
tokenizer.MaxEncodingTokens = 4096; // Default is 2048

// Count tokens for longer texts
int tokenCount = tokenizer.CountTokens("Very long document text...");
```

## API Reference

### BertTokenizer

**Creation**
```csharp
static Task<BertTokenizer> CreateAsync()
static Task<BertTokenizer> CreateAsync(FastBertTokenizer.BertTokenizer tokenizer)
```

**Properties**
- `MaxEncodingTokens`: Maximum tokens allowed during encoding (default: 2048)

**Methods**
- `CountTokens(string text)`: Count tokens in text
- `Encode(string text)`: Encode text to token array
- `Decode(int[] tokens)`: Decode tokens back to text

## Implementation Details

- **Vocabulary**: Uses BERT base uncased model from Hugging Face
- **Token Range**: Handles standard BERT vocabulary (30,522 tokens)
- **Truncation**: Automatically truncates at `MaxEncodingTokens` limit
- **Performance**: Optimized for repeated tokenization operations
- **Memory**: Loads vocabulary once during initialization

## Dependencies

- **.NET 8.0+**: Modern async/await patterns
- **EasyReasy.KnowledgeBase**: Core interfaces (`ITokenizer`)
- **FastBertTokenizer**: High-performance BERT tokenization library

## License

MIT