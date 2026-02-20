using System.Net;
using System.Text.Json;
using ContentUnderstanding.Client;
using ContentUnderstanding.Client.Models;

namespace ContentUnderstanding.Client.Tests;

public class ContentUnderstandingClientTests : IDisposable
{
    private const string TestEndpoint = "https://test.cognitiveservices.azure.com";
    private const string TestApiKey = "test-api-key";
    private const string TestAnalyzerId = "test-analyzer";

    private readonly MockHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;
    private readonly ContentUnderstandingClient _client;

    public ContentUnderstandingClientTests()
    {
        _handler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_handler);
        _client = new ContentUnderstandingClient(TestEndpoint, TestApiKey, "2024-12-01-preview", _httpClient);
    }

    [Fact]
    public void Constructor_NullOrEmptyEndpoint_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ContentUnderstandingClient("", TestApiKey));
        Assert.Throws<ArgumentException>(() => new ContentUnderstandingClient("  ", TestApiKey));
    }

    [Fact]
    public void Constructor_NullOrEmptyApiKey_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ContentUnderstandingClient(TestEndpoint, ""));
        Assert.Throws<ArgumentException>(() => new ContentUnderstandingClient(TestEndpoint, "  "));
    }

    [Fact]
    public async Task CreateOrReplaceAnalyzerAsync_SendsCorrectRequest()
    {
        var analyzerResponse = new AnalyzerResponse
        {
            AnalyzerId = TestAnalyzerId,
            Description = "Test analyzer"
        };

        _handler.SetResponse(HttpStatusCode.Created, JsonSerializer.Serialize(analyzerResponse));

        var definition = new AnalyzerDefinition
        {
            Description = "Test analyzer",
            Scenario = "document"
        };

        var result = await _client.CreateOrReplaceAnalyzerAsync(TestAnalyzerId, definition);

        Assert.Equal(TestAnalyzerId, result.AnalyzerId);
        Assert.Equal("Test analyzer", result.Description);
        Assert.Equal(HttpMethod.Put, _handler.LastRequest?.Method);
        Assert.Contains($"/contentunderstanding/analyzers/{TestAnalyzerId}", _handler.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task GetAnalyzerAsync_ReturnsAnalyzerDetails()
    {
        var analyzerResponse = new AnalyzerResponse
        {
            AnalyzerId = TestAnalyzerId,
            Description = "Test analyzer",
            Scenario = "document"
        };

        _handler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(analyzerResponse));

        var result = await _client.GetAnalyzerAsync(TestAnalyzerId);

        Assert.Equal(TestAnalyzerId, result.AnalyzerId);
        Assert.Equal(HttpMethod.Get, _handler.LastRequest?.Method);
    }

    [Fact]
    public async Task DeleteAnalyzerAsync_SendsDeleteRequest()
    {
        _handler.SetResponse(HttpStatusCode.NoContent, "");

        await _client.DeleteAnalyzerAsync(TestAnalyzerId);

        Assert.Equal(HttpMethod.Delete, _handler.LastRequest?.Method);
        Assert.Contains($"/contentunderstanding/analyzers/{TestAnalyzerId}", _handler.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task AnalyzeContentFromUrlAsync_SubmitsAndPollsForResult()
    {
        var completedResult = new AnalyzeResult
        {
            Id = "result-123",
            Status = "Succeeded",
            Result = new AnalyzeResultContent
            {
                AnalyzerId = TestAnalyzerId,
                Contents = new List<ContentItem>
                {
                    new() { Markdown = "Extracted text content" }
                }
            }
        };

        _handler.SetupAnalyzeWithPolling(
            "https://test.cognitiveservices.azure.com/operations/op-123",
            completedResult);

        var result = await _client.AnalyzeContentFromUrlAsync(
            TestAnalyzerId,
            "https://example.com/document.pdf",
            TimeSpan.FromMilliseconds(10));

        Assert.Equal("Succeeded", result.Status);
        Assert.NotNull(result.Result);
        Assert.Single(result.Result.Contents!);
    }

    [Fact]
    public async Task AnalyzeContentFromFileAsync_ReadsFileAndSubmits()
    {
        // Create a temp file
        var tempFile = Path.GetTempFileName() + ".pdf";
        await File.WriteAllBytesAsync(tempFile, new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF header

        try
        {
            var completedResult = new AnalyzeResult
            {
                Id = "result-456",
                Status = "Succeeded"
            };

            _handler.SetupAnalyzeWithPolling(
                "https://test.cognitiveservices.azure.com/operations/op-456",
                completedResult);

            var result = await _client.AnalyzeContentFromFileAsync(
                TestAnalyzerId,
                tempFile,
                TimeSpan.FromMilliseconds(10));

            Assert.Equal("Succeeded", result.Status);

            // Verify the POST request contained base64 data
            var requestBody = _handler.LastPostBody;
            Assert.NotNull(requestBody);
            Assert.Contains("data", requestBody);
            Assert.Contains("mimeType", requestBody);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task AnalyzeContentFromFileAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _client.AnalyzeContentFromFileAsync(TestAnalyzerId, "/nonexistent/file.pdf"));
    }

    [Fact]
    public async Task AnalyzeFilesInDirectoryAsync_ProcessesAllFiles()
    {
        // Create a temp directory with files
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "file1.txt"), "content1");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "file2.txt"), "content2");

            var completedResult = new AnalyzeResult
            {
                Id = "result-789",
                Status = "Succeeded"
            };

            _handler.SetupAnalyzeWithPolling(
                "https://test.cognitiveservices.azure.com/operations/op-789",
                completedResult);

            var results = await _client.AnalyzeFilesInDirectoryAsync(
                TestAnalyzerId,
                tempDir,
                "*.txt",
                pollingInterval: TimeSpan.FromMilliseconds(10));

            Assert.Equal(2, results.Count);
            Assert.All(results.Values, r => Assert.Equal("Succeeded", r.Status));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeFilesInDirectoryAsync_DirectoryNotFound_ThrowsDirectoryNotFoundException()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => _client.AnalyzeFilesInDirectoryAsync(TestAnalyzerId, "/nonexistent/directory"));
    }

    [Fact]
    public async Task ApiError_ThrowsContentUnderstandingException()
    {
        _handler.SetResponse(HttpStatusCode.BadRequest, "{\"error\":{\"code\":\"InvalidRequest\",\"message\":\"Bad request\"}}");

        var ex = await Assert.ThrowsAsync<ContentUnderstandingException>(
            () => _client.GetAnalyzerAsync(TestAnalyzerId));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("InvalidRequest", ex.ResponseBody);
    }

    [Fact]
    public async Task AnalyzeContentFromUrlAsync_ApiReturnsFailedStatus_ReturnsFailedResult()
    {
        var failedResult = new AnalyzeResult
        {
            Id = "result-err",
            Status = "Failed",
            Error = new AnalyzeError
            {
                Code = "ProcessingError",
                Message = "Failed to process content"
            }
        };

        _handler.SetupAnalyzeWithPolling(
            "https://test.cognitiveservices.azure.com/operations/op-err",
            failedResult);

        var result = await _client.AnalyzeContentFromUrlAsync(
            TestAnalyzerId,
            "https://example.com/bad.pdf",
            TimeSpan.FromMilliseconds(10));

        Assert.Equal("Failed", result.Status);
        Assert.NotNull(result.Error);
        Assert.Equal("ProcessingError", result.Error.Code);
    }

    public void Dispose()
    {
        _client.Dispose();
        _httpClient.Dispose();
    }
}

/// <summary>
/// Mock HTTP handler for testing HTTP interactions without making real API calls.
/// </summary>
internal class MockHttpMessageHandler : HttpMessageHandler
{
    private HttpStatusCode _statusCode = HttpStatusCode.OK;
    private string _responseBody = "";
    private string? _operationLocation;
    private string? _pollingResponseBody;
    private int _requestCount;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastPostBody { get; private set; }

    public void SetResponse(HttpStatusCode statusCode, string responseBody)
    {
        _statusCode = statusCode;
        _responseBody = responseBody;
        _operationLocation = null;
    }

    public void SetupAnalyzeWithPolling(string operationLocation, AnalyzeResult completedResult)
    {
        _statusCode = HttpStatusCode.Accepted;
        _responseBody = JsonSerializer.Serialize(new { id = completedResult.Id, status = "NotStarted" });
        _operationLocation = operationLocation;
        _pollingResponseBody = JsonSerializer.Serialize(completedResult);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        _requestCount++;

        if (request.Content is not null)
        {
            LastPostBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        // If this is a polling request (GET to operation location)
        if (_operationLocation is not null &&
            request.Method == HttpMethod.Get &&
            request.RequestUri?.ToString() == _operationLocation)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_pollingResponseBody ?? "{}")
            };
        }

        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody)
        };

        if (_operationLocation is not null)
        {
            response.Headers.Add("Operation-Location", _operationLocation);
        }

        return response;
    }
}
