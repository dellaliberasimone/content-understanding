# Azure AI Content Understanding Client

A custom .NET client library for interacting with the [Azure AI Content Understanding REST API](https://learn.microsoft.com/en-us/rest/api/content-understanding/). This client supports analyzing content from URLs and **local files/directories**.

## Features

- **Create, retrieve, and delete analyzers** for document, image, audio, and video content
- **Analyze content from URLs** by providing a public content URL
- **Analyze content from local files** by reading files and sending them as base64-encoded data
- **Batch-process files in a local directory** with configurable search patterns and subdirectory support
- **Automatic result polling** with configurable intervals
- Built-in MIME type detection for common file formats
- Strongly typed request/response models

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) or later
- An Azure subscription with an [Azure AI Content Understanding](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/) resource
- Your resource endpoint and API key

## Getting Started

### 1. Build the project

```bash
dotnet build
```

### 2. Run the tests

```bash
dotnet test
```

### 3. Reference the library

Add a project reference to `ContentUnderstanding.Client` from your application:

```bash
dotnet add reference ../ContentUnderstanding.Client/ContentUnderstanding.Client.csproj
```

## Usage

### Initialize the client

```csharp
using ContentUnderstanding.Client;

var endpoint = "https://<your-resource>.cognitiveservices.azure.com";
var apiKey = "<your-api-key>";

using var client = new ContentUnderstandingClient(endpoint, apiKey);
```

### Create an analyzer

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
            ["InvoiceDate"] = new() { Type = "date", Description = "Date of the invoice" }
        }
    }
};

var analyzer = await client.CreateOrReplaceAnalyzerAsync("my-invoice-analyzer", definition);
```

### Analyze content from a URL

```csharp
var result = await client.AnalyzeContentFromUrlAsync(
    "my-invoice-analyzer",
    "https://example.com/invoice.pdf");

Console.WriteLine($"Status: {result.Status}");
```

### Analyze a local file

```csharp
var result = await client.AnalyzeContentFromFileAsync(
    "my-invoice-analyzer",
    "/path/to/invoice.pdf");

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
    "my-invoice-analyzer",
    "/path/to/invoices",
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

## API Reference

### `ContentUnderstandingClient`

| Method | Description |
|--------|-------------|
| `CreateOrReplaceAnalyzerAsync` | Creates or replaces an analyzer with the given definition |
| `GetAnalyzerAsync` | Retrieves configuration of an existing analyzer |
| `DeleteAnalyzerAsync` | Deletes an analyzer |
| `AnalyzeContentFromUrlAsync` | Analyzes content at a given URL |
| `AnalyzeContentFromFileAsync` | Reads a local file and analyzes it |
| `AnalyzeFilesInDirectoryAsync` | Processes all matching files in a directory |

### Configuration

The client accepts an optional `apiVersion` parameter (default: `2024-12-01-preview`):

```csharp
var client = new ContentUnderstandingClient(endpoint, apiKey, apiVersion: "2025-05-01-preview");
```

## Project Structure

```
├── ContentUnderstanding.Client/          # Client library
│   ├── ContentUnderstandingClient.cs     # Main client class
│   ├── ContentUnderstandingException.cs  # Custom exception type
│   ├── MimeTypeHelper.cs                # MIME type resolution
│   └── Models/                          # Request/response models
│       ├── AnalyzerDefinition.cs
│       ├── AnalyzerResponse.cs
│       ├── AnalyzeRequest.cs
│       ├── AnalyzeResult.cs
│       └── FieldSchema.cs
├── ContentUnderstanding.Client.Tests/    # Unit tests
│   ├── ContentUnderstandingClientTests.cs
│   └── MimeTypeHelperTests.cs
└── ContentUnderstanding.slnx             # Solution file
```

## License

This project is provided as-is for educational and development purposes.
