# Azure AI Content Understanding Client SDK

A .NET client library for interacting with the [Azure AI Content Understanding REST API](https://learn.microsoft.com/en-us/rest/api/content-understanding/). Provides strongly typed methods for every prebuilt analyzer plus full support for custom analyzers.

## Features

- **Entra ID authentication** (preferred) via `DefaultAzureCredential` or any `TokenCredential`, with API key fallback
- **Prebuilt analyzer methods** for document, image, audio, and video content
- **Domain-specific analyzers** for invoices, receipts, and identity documents
- **Search / RAG analyzers** for semantic search ingestion across all modalities
- **Custom analyzers** with full field-schema support (classification, generation, nested objects/arrays)
- **Analyze from URLs or local files**, with batch directory processing
- **Automatic result polling** with configurable intervals
- Built-in MIME type detection for common file formats

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) or later
- An Azure subscription with an [Azure AI Content Understanding](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/) resource
- Your resource endpoint (and optionally an API key — Entra ID is preferred)

## Getting Started

### 1. Build the project

```bash
dotnet build
```

### 2. Reference the library

Add a project reference to `ContentUnderstanding.Client` from your application:

```bash
dotnet add reference ../ContentUnderstanding.Client/ContentUnderstanding.Client.csproj
```

## Usage

### Initialize the client (Entra ID — recommended)

```csharp
using ContentUnderstanding.Client;

var endpoint = "https://<your-resource>.cognitiveservices.azure.com";

// Uses DefaultAzureCredential (managed identity, Azure CLI, VS, etc.)
using var client = new ContentUnderstandingClient(endpoint);
```

Or with a specific `TokenCredential`:

```csharp
using Azure.Identity;

using var client = new ContentUnderstandingClient(endpoint, new ManagedIdentityCredential());
```

### Initialize the client (API key)

```csharp
var apiKey = "<your-api-key>";
using var client = new ContentUnderstandingClient(endpoint, apiKey);
```

### Analyze a document (prebuilt)

```csharp
// OCR — extract text from a document
var result = await client.ReadDocumentFromUrlAsync("https://example.com/document.pdf");

// Layout — extract tables, figures, sections
var layout = await client.ExtractLayoutFromFileAsync("/path/to/document.pdf");

// General document analysis
var doc = await client.AnalyzeDocumentFromUrlAsync("https://example.com/report.pdf");
```

### Analyze an image

```csharp
var result = await client.AnalyzeImageFromFileAsync("/path/to/photo.jpg");
```

### Analyze audio

```csharp
var result = await client.AnalyzeAudioFromUrlAsync("https://example.com/recording.wav");
```

### Analyze video

```csharp
var result = await client.AnalyzeVideoFromFileAsync("/path/to/clip.mp4");
```

### Domain-specific analysis

```csharp
// Invoices
var invoice = await client.AnalyzeInvoiceFromFileAsync("/path/to/invoice.pdf");

// Receipts
var receipt = await client.AnalyzeReceiptFromFileAsync("/path/to/receipt.jpg");

// Identity documents
var id = await client.AnalyzeIdDocumentFromFileAsync("/path/to/id-card.png");
```

### Search / RAG analysis

```csharp
var docSearch = await client.AnalyzeDocumentForSearchFromUrlAsync("https://example.com/manual.pdf");
var imgSearch = await client.AnalyzeImageForSearchFromFileAsync("/path/to/diagram.png");
var audioSearch = await client.AnalyzeAudioForSearchFromUrlAsync("https://example.com/call.wav");
var videoSearch = await client.AnalyzeVideoForSearchFromFileAsync("/path/to/presentation.mp4");
```

### Create a custom analyzer

```csharp
using ContentUnderstanding.Client.Models;

var definition = new AnalyzerDefinition
{
    Description = "Invoice field extractor",
    Scenario = "document",
    FieldSchema = new FieldSchema
    {
        Fields = new Dictionary<string, FieldDefinition>
        {
            ["InvoiceNumber"] = new() { Type = "string", Description = "The invoice number" },
            ["TotalAmount"] = new() { Type = "number", Description = "Total amount due" },
            ["Category"] = new()
            {
                Type = "string",
                Method = "classify",
                Description = "Invoice category",
                EnumValues = ["services", "goods", "subscription"]
            },
            ["LineItems"] = new()
            {
                Type = "array",
                Description = "Line items on the invoice",
                Items = new FieldDefinition
                {
                    Type = "object",
                    Properties = new Dictionary<string, FieldDefinition>
                    {
                        ["description"] = new() { Type = "string" },
                        ["amount"] = new() { Type = "number" }
                    }
                }
            }
        }
    }
};

var analyzer = await client.CreateOrReplaceAnalyzerAsync("my-invoice-analyzer", definition);
```

### Use any analyzer ID directly

```csharp
var result = await client.AnalyzeContentFromUrlAsync(
    "my-invoice-analyzer",
    "https://example.com/invoice.pdf");

if (result.Status == "Succeeded" && result.Result?.Contents != null)
{
    foreach (var content in result.Result.Contents)
    {
        Console.WriteLine(content.Markdown);

        if (content.Fields != null)
        {
            foreach (var (name, value) in content.Fields)
            {
                Console.WriteLine($"  {name}: {value.ValueString}");
            }
        }
    }
}
```

### Process all files in a directory

```csharp
var results = await client.AnalyzeFilesInDirectoryAsync(
    PrebuiltAnalyzers.Document,
    "/path/to/documents",
    searchPattern: "*.pdf",
    includeSubdirectories: true);

foreach (var (filePath, result) in results)
{
    Console.WriteLine($"{filePath}: {result.Status}");
}
```

### Delete an analyzer

```csharp
await client.DeleteAnalyzerAsync("my-invoice-analyzer");
```

## Prebuilt Analyzer Constants

The `PrebuiltAnalyzers` class provides constants for all available prebuilt analyzers:

| Constant | Analyzer ID | Description |
|----------|-------------|-------------|
| `PrebuiltAnalyzers.Read` | `prebuilt-read` | OCR text extraction |
| `PrebuiltAnalyzers.Layout` | `prebuilt-layout` | Layout and structure extraction |
| `PrebuiltAnalyzers.Document` | `prebuilt-document` | General document analysis |
| `PrebuiltAnalyzers.Image` | `prebuilt-image` | Image analysis |
| `PrebuiltAnalyzers.Audio` | `prebuilt-audio` | Audio analysis |
| `PrebuiltAnalyzers.Video` | `prebuilt-video` | Video analysis |
| `PrebuiltAnalyzers.DocumentSearch` | `prebuilt-documentSearch` | Document search / RAG |
| `PrebuiltAnalyzers.ImageSearch` | `prebuilt-imageSearch` | Image search / RAG |
| `PrebuiltAnalyzers.AudioSearch` | `prebuilt-audioSearch` | Audio search / RAG |
| `PrebuiltAnalyzers.VideoSearch` | `prebuilt-videoSearch` | Video search / RAG |
| `PrebuiltAnalyzers.Invoice` | `prebuilt-invoice` | Invoice extraction |
| `PrebuiltAnalyzers.Receipt` | `prebuilt-receipt` | Receipt extraction |
| `PrebuiltAnalyzers.IdDocument` | `prebuilt-idDocument` | Identity document extraction |

## API Reference

### `ContentUnderstandingClient`

| Method | Description |
|--------|-------------|
| **Analyzer Management** | |
| `CreateOrReplaceAnalyzerAsync` | Creates or replaces an analyzer |
| `GetAnalyzerAsync` | Retrieves an analyzer's configuration |
| `DeleteAnalyzerAsync` | Deletes an analyzer |
| **Generic Analysis** | |
| `AnalyzeContentFromUrlAsync` | Analyzes content at a URL with any analyzer |
| `AnalyzeContentFromFileAsync` | Analyzes a local file with any analyzer |
| `AnalyzeFilesInDirectoryAsync` | Batch-processes files in a directory |
| **Document** | |
| `ReadDocumentFromUrlAsync` / `…FromFileAsync` | OCR text extraction |
| `ExtractLayoutFromUrlAsync` / `…FromFileAsync` | Layout / structure extraction |
| `AnalyzeDocumentFromUrlAsync` / `…FromFileAsync` | General document analysis |
| **Image** | |
| `AnalyzeImageFromUrlAsync` / `…FromFileAsync` | Image analysis |
| **Audio** | |
| `AnalyzeAudioFromUrlAsync` / `…FromFileAsync` | Audio analysis |
| **Video** | |
| `AnalyzeVideoFromUrlAsync` / `…FromFileAsync` | Video analysis |
| **Search / RAG** | |
| `AnalyzeDocumentForSearchFromUrlAsync` / `…FromFileAsync` | Document search |
| `AnalyzeImageForSearchFromUrlAsync` / `…FromFileAsync` | Image search |
| `AnalyzeAudioForSearchFromUrlAsync` / `…FromFileAsync` | Audio search |
| `AnalyzeVideoForSearchFromUrlAsync` / `…FromFileAsync` | Video search |
| **Domain-Specific** | |
| `AnalyzeInvoiceFromUrlAsync` / `…FromFileAsync` | Invoice extraction |
| `AnalyzeReceiptFromUrlAsync` / `…FromFileAsync` | Receipt extraction |
| `AnalyzeIdDocumentFromUrlAsync` / `…FromFileAsync` | ID document extraction |

### Configuration

The client accepts an optional `apiVersion` parameter (default: `2024-12-01-preview`):

```csharp
var client = new ContentUnderstandingClient(endpoint, apiVersion: "2025-05-01-preview");
```

## Project Structure

```
├── ContentUnderstanding.Client/          # Client library
│   ├── ContentUnderstandingClient.cs     # Main client class
│   ├── ContentUnderstandingException.cs  # Custom exception type
│   ├── MimeTypeHelper.cs                # MIME type resolution
│   ├── PrebuiltAnalyzers.cs             # Prebuilt analyzer constants
│   └── Models/                          # Request/response models
│       ├── AnalyzerDefinition.cs
│       ├── AnalyzerResponse.cs
│       ├── AnalyzeRequest.cs
│       ├── AnalyzeResult.cs
│       └── FieldSchema.cs
└── ContentUnderstanding.slnx             # Solution file
```

## License

This project is provided as-is for educational and development purposes.
