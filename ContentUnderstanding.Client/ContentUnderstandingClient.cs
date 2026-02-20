using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using ContentUnderstanding.Client.Models;

namespace ContentUnderstanding.Client;

/// <summary>
/// A custom client for interacting with the Azure AI Content Understanding REST API.
/// Supports analyzing content from URLs and local files using either Entra ID or API key authentication.
/// </summary>
public class ContentUnderstandingClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _apiVersion;
    private readonly bool _ownsHttpClient;
    private readonly TokenCredential? _credential;

    private static readonly string[] CognitiveServicesScope = ["https://cognitiveservices.azure.com/.default"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new instance of <see cref="ContentUnderstandingClient"/>
    /// using Entra ID authentication with <see cref="DefaultAzureCredential"/>.
    /// This is the preferred authentication method.
    /// </summary>
    /// <param name="endpoint">The Azure AI Content Understanding service endpoint URL.</param>
    /// <param name="apiVersion">The API version to use (default: 2024-12-01-preview).</param>
    public ContentUnderstandingClient(string endpoint, string apiVersion = "2024-12-01-preview")
        : this(endpoint, new DefaultAzureCredential(), apiVersion)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ContentUnderstandingClient"/>
    /// using the specified <see cref="TokenCredential"/> for Entra ID authentication.
    /// </summary>
    /// <param name="endpoint">The Azure AI Content Understanding service endpoint URL.</param>
    /// <param name="credential">The token credential for authentication.</param>
    /// <param name="apiVersion">The API version to use (default: 2024-12-01-preview).</param>
    public ContentUnderstandingClient(string endpoint, TokenCredential credential, string apiVersion = "2024-12-01-preview")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiVersion);

        _endpoint = endpoint.TrimEnd('/');
        _apiVersion = apiVersion;
        _credential = credential;
        _httpClient = new HttpClient();
        _ownsHttpClient = true;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ContentUnderstandingClient"/>
    /// using the specified endpoint and API key.
    /// Prefer using the Entra ID constructor when possible.
    /// </summary>
    /// <param name="endpoint">The Azure AI Content Understanding service endpoint URL.</param>
    /// <param name="apiKey">The subscription key for authentication.</param>
    /// <param name="apiVersion">The API version to use (default: 2024-12-01-preview).</param>
    public ContentUnderstandingClient(string endpoint, string apiKey, string apiVersion = "2024-12-01-preview")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiVersion);

        _endpoint = endpoint.TrimEnd('/');
        _apiVersion = apiVersion;
        _httpClient = new HttpClient();
        _ownsHttpClient = true;
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
    }

    // ── Analyzer Management ─────────────────────────────────────────────

    /// <summary>
    /// Creates or replaces an analyzer with the given ID and definition.
    /// </summary>
    public async Task<AnalyzerResponse> CreateOrReplaceAnalyzerAsync(
        string analyzerId,
        AnalyzerDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analyzerId);
        ArgumentNullException.ThrowIfNull(definition);

        var url = $"{_endpoint}/contentunderstanding/analyzers/{Uri.EscapeDataString(analyzerId)}?api-version={_apiVersion}";
        var json = JsonSerializer.Serialize(definition, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = content };
        await ApplyAuthAsync(request, cancellationToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<AnalyzerResponse>(responseJson, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize analyzer response.");
    }

    /// <summary>
    /// Retrieves the configuration of an existing analyzer.
    /// </summary>
    public async Task<AnalyzerResponse> GetAnalyzerAsync(
        string analyzerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analyzerId);

        var url = $"{_endpoint}/contentunderstanding/analyzers/{Uri.EscapeDataString(analyzerId)}?api-version={_apiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        await ApplyAuthAsync(request, cancellationToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<AnalyzerResponse>(responseJson, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize analyzer response.");
    }

    /// <summary>
    /// Deletes an existing analyzer.
    /// </summary>
    public async Task DeleteAnalyzerAsync(
        string analyzerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analyzerId);

        var url = $"{_endpoint}/contentunderstanding/analyzers/{Uri.EscapeDataString(analyzerId)}?api-version={_apiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        await ApplyAuthAsync(request, cancellationToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    // ── Generic Analysis ────────────────────────────────────────────────

    /// <summary>
    /// Submits content at the given URL for analysis and returns the result
    /// after polling until the operation completes.
    /// </summary>
    public async Task<AnalyzeResult> AnalyzeContentFromUrlAsync(
        string analyzerId,
        string contentUrl,
        TimeSpan? pollingInterval = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analyzerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentUrl);

        var analyzeRequest = new AnalyzeRequest { Url = contentUrl };
        return await AnalyzeContentAsync(analyzerId, analyzeRequest, pollingInterval, cancellationToken);
    }

    /// <summary>
    /// Reads a local file and submits it for analysis, returning the result
    /// after polling until the operation completes.
    /// </summary>
    public async Task<AnalyzeResult> AnalyzeContentFromFileAsync(
        string analyzerId,
        string filePath,
        TimeSpan? pollingInterval = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analyzerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"The file '{filePath}' was not found.", filePath);
        }

        var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var base64Content = Convert.ToBase64String(fileBytes);
        var mimeType = MimeTypeHelper.GetMimeType(filePath);

        var analyzeRequest = new AnalyzeRequest
        {
            Data = base64Content,
            MimeType = mimeType
        };

        return await AnalyzeContentAsync(analyzerId, analyzeRequest, pollingInterval, cancellationToken);
    }

    /// <summary>
    /// Processes all supported files in a local directory, analyzing each one
    /// with the specified analyzer.
    /// </summary>
    /// <param name="analyzerId">The analyzer to use for processing.</param>
    /// <param name="directoryPath">Path to the local directory containing files.</param>
    /// <param name="searchPattern">File search pattern (default: "*.*").</param>
    /// <param name="includeSubdirectories">Whether to search subdirectories.</param>
    /// <param name="pollingInterval">Interval between result polling attempts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping file paths to their analysis results.</returns>
    public async Task<Dictionary<string, AnalyzeResult>> AnalyzeFilesInDirectoryAsync(
        string analyzerId,
        string directoryPath,
        string searchPattern = "*.*",
        bool includeSubdirectories = false,
        TimeSpan? pollingInterval = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analyzerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"The directory '{directoryPath}' was not found.");
        }

        var searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(directoryPath, searchPattern, searchOption);

        var results = new Dictionary<string, AnalyzeResult>();

        foreach (var file in files)
        {
            var result = await AnalyzeContentFromFileAsync(analyzerId, file, pollingInterval, cancellationToken);
            results[file] = result;
        }

        return results;
    }

    // ── Document Analysis ───────────────────────────────────────────────

    /// <summary>
    /// Performs OCR to extract text (words, paragraphs, formulas, barcodes) from a document URL.
    /// Uses the <c>prebuilt-read</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> ReadDocumentFromUrlAsync(
        string contentUrl, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromUrlAsync(PrebuiltAnalyzers.Read, contentUrl, pollingInterval, cancellationToken);

    /// <summary>
    /// Performs OCR to extract text from a local document file.
    /// Uses the <c>prebuilt-read</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> ReadDocumentFromFileAsync(
        string filePath, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromFileAsync(PrebuiltAnalyzers.Read, filePath, pollingInterval, cancellationToken);

    /// <summary>
    /// Extracts content with layout and structure understanding (tables, figures, sections)
    /// from a document URL. Uses the <c>prebuilt-layout</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> ExtractLayoutFromUrlAsync(
        string contentUrl, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromUrlAsync(PrebuiltAnalyzers.Layout, contentUrl, pollingInterval, cancellationToken);

    /// <summary>
    /// Extracts content with layout and structure understanding from a local file.
    /// Uses the <c>prebuilt-layout</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> ExtractLayoutFromFileAsync(
        string filePath, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromFileAsync(PrebuiltAnalyzers.Layout, filePath, pollingInterval, cancellationToken);

    /// <summary>
    /// Analyzes a document from a URL using the general-purpose <c>prebuilt-document</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeDocumentFromUrlAsync(
        string contentUrl, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromUrlAsync(PrebuiltAnalyzers.Document, contentUrl, pollingInterval, cancellationToken);

    /// <summary>
    /// Analyzes a local document file using the general-purpose <c>prebuilt-document</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeDocumentFromFileAsync(
        string filePath, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromFileAsync(PrebuiltAnalyzers.Document, filePath, pollingInterval, cancellationToken);

    // ── Image Analysis ──────────────────────────────────────────────────

    /// <summary>
    /// Analyzes an image from a URL using the <c>prebuilt-image</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeImageFromUrlAsync(
        string contentUrl, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromUrlAsync(PrebuiltAnalyzers.Image, contentUrl, pollingInterval, cancellationToken);

    /// <summary>
    /// Analyzes a local image file using the <c>prebuilt-image</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeImageFromFileAsync(
        string filePath, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromFileAsync(PrebuiltAnalyzers.Image, filePath, pollingInterval, cancellationToken);

    // ── Audio Analysis ──────────────────────────────────────────────────

    /// <summary>
    /// Analyzes audio from a URL using the <c>prebuilt-audio</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeAudioFromUrlAsync(
        string contentUrl, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromUrlAsync(PrebuiltAnalyzers.Audio, contentUrl, pollingInterval, cancellationToken);

    /// <summary>
    /// Analyzes a local audio file using the <c>prebuilt-audio</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeAudioFromFileAsync(
        string filePath, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromFileAsync(PrebuiltAnalyzers.Audio, filePath, pollingInterval, cancellationToken);

    // ── Video Analysis ──────────────────────────────────────────────────

    /// <summary>
    /// Analyzes a video from a URL using the <c>prebuilt-video</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeVideoFromUrlAsync(
        string contentUrl, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromUrlAsync(PrebuiltAnalyzers.Video, contentUrl, pollingInterval, cancellationToken);

    /// <summary>
    /// Analyzes a local video file using the <c>prebuilt-video</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeVideoFromFileAsync(
        string filePath, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromFileAsync(PrebuiltAnalyzers.Video, filePath, pollingInterval, cancellationToken);

    // ── Search / RAG Analysis ───────────────────────────────────────────

    /// <summary>
    /// Performs semantic document search extraction from a URL.
    /// Uses the <c>prebuilt-documentSearch</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeDocumentForSearchFromUrlAsync(
        string contentUrl, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromUrlAsync(PrebuiltAnalyzers.DocumentSearch, contentUrl, pollingInterval, cancellationToken);

    /// <summary>
    /// Performs semantic document search extraction from a local file.
    /// Uses the <c>prebuilt-documentSearch</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeDocumentForSearchFromFileAsync(
        string filePath, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromFileAsync(PrebuiltAnalyzers.DocumentSearch, filePath, pollingInterval, cancellationToken);

    /// <summary>
    /// Performs image search extraction from a URL.
    /// Uses the <c>prebuilt-imageSearch</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeImageForSearchFromUrlAsync(
        string contentUrl, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromUrlAsync(PrebuiltAnalyzers.ImageSearch, contentUrl, pollingInterval, cancellationToken);

    /// <summary>
    /// Performs image search extraction from a local file.
    /// Uses the <c>prebuilt-imageSearch</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeImageForSearchFromFileAsync(
        string filePath, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromFileAsync(PrebuiltAnalyzers.ImageSearch, filePath, pollingInterval, cancellationToken);

    /// <summary>
    /// Performs audio search extraction from a URL.
    /// Uses the <c>prebuilt-audioSearch</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeAudioForSearchFromUrlAsync(
        string contentUrl, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromUrlAsync(PrebuiltAnalyzers.AudioSearch, contentUrl, pollingInterval, cancellationToken);

    /// <summary>
    /// Performs audio search extraction from a local file.
    /// Uses the <c>prebuilt-audioSearch</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeAudioForSearchFromFileAsync(
        string filePath, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromFileAsync(PrebuiltAnalyzers.AudioSearch, filePath, pollingInterval, cancellationToken);

    /// <summary>
    /// Performs video search extraction from a URL.
    /// Uses the <c>prebuilt-videoSearch</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeVideoForSearchFromUrlAsync(
        string contentUrl, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromUrlAsync(PrebuiltAnalyzers.VideoSearch, contentUrl, pollingInterval, cancellationToken);

    /// <summary>
    /// Performs video search extraction from a local file.
    /// Uses the <c>prebuilt-videoSearch</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeVideoForSearchFromFileAsync(
        string filePath, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromFileAsync(PrebuiltAnalyzers.VideoSearch, filePath, pollingInterval, cancellationToken);

    // ── Domain-Specific Analysis ────────────────────────────────────────

    /// <summary>
    /// Extracts invoice data from a URL using the <c>prebuilt-invoice</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeInvoiceFromUrlAsync(
        string contentUrl, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromUrlAsync(PrebuiltAnalyzers.Invoice, contentUrl, pollingInterval, cancellationToken);

    /// <summary>
    /// Extracts invoice data from a local file using the <c>prebuilt-invoice</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeInvoiceFromFileAsync(
        string filePath, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromFileAsync(PrebuiltAnalyzers.Invoice, filePath, pollingInterval, cancellationToken);

    /// <summary>
    /// Extracts receipt data from a URL using the <c>prebuilt-receipt</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeReceiptFromUrlAsync(
        string contentUrl, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromUrlAsync(PrebuiltAnalyzers.Receipt, contentUrl, pollingInterval, cancellationToken);

    /// <summary>
    /// Extracts receipt data from a local file using the <c>prebuilt-receipt</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeReceiptFromFileAsync(
        string filePath, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromFileAsync(PrebuiltAnalyzers.Receipt, filePath, pollingInterval, cancellationToken);

    /// <summary>
    /// Extracts identity document data from a URL using the <c>prebuilt-idDocument</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeIdDocumentFromUrlAsync(
        string contentUrl, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromUrlAsync(PrebuiltAnalyzers.IdDocument, contentUrl, pollingInterval, cancellationToken);

    /// <summary>
    /// Extracts identity document data from a local file using the <c>prebuilt-idDocument</c> analyzer.
    /// </summary>
    public Task<AnalyzeResult> AnalyzeIdDocumentFromFileAsync(
        string filePath, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => AnalyzeContentFromFileAsync(PrebuiltAnalyzers.IdDocument, filePath, pollingInterval, cancellationToken);

    // ── Core Implementation ─────────────────────────────────────────────

    private async Task<AnalyzeResult> AnalyzeContentAsync(
        string analyzerId,
        AnalyzeRequest analyzeRequest,
        TimeSpan? pollingInterval,
        CancellationToken cancellationToken)
    {
        var interval = pollingInterval ?? TimeSpan.FromSeconds(2);

        var url = $"{_endpoint}/contentunderstanding/analyzers/{Uri.EscapeDataString(analyzerId)}:analyze?api-version={_apiVersion}";
        var json = JsonSerializer.Serialize(analyzeRequest, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        await ApplyAuthAsync(request, cancellationToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        // The API returns 202 Accepted with an Operation-Location header for polling.
        if (!response.Headers.TryGetValues("Operation-Location", out var locationValues))
        {
            // If no polling header, try to read the result directly from the response body.
            var directJson = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<AnalyzeResult>(directJson, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize analysis result.");
        }

        var operationUrl = locationValues.First();

        // Poll until the operation completes.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(interval, cancellationToken);

            using var pollRequest = new HttpRequestMessage(HttpMethod.Get, operationUrl);
            await ApplyAuthAsync(pollRequest, cancellationToken);

            using var pollResponse = await _httpClient.SendAsync(pollRequest, cancellationToken);
            await EnsureSuccessAsync(pollResponse, cancellationToken);

            var resultJson = await pollResponse.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<AnalyzeResult>(resultJson, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize polling result.");

            if (result.Status is "Succeeded" or "Failed" or "Canceled")
            {
                return result;
            }
        }
    }

    private async Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_credential is not null)
        {
            var tokenRequestContext = new TokenRequestContext(CognitiveServicesScope);
            var accessToken = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ContentUnderstandingException(
                $"API request failed with status {(int)response.StatusCode} ({response.StatusCode}): {body}",
                response.StatusCode,
                body);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
