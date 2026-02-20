using ContentUnderstanding.Client;

namespace ContentUnderstanding.Client.Tests;

public class MimeTypeHelperTests
{
    [Theory]
    [InlineData("document.pdf", "application/pdf")]
    [InlineData("image.jpg", "image/jpeg")]
    [InlineData("image.jpeg", "image/jpeg")]
    [InlineData("image.png", "image/png")]
    [InlineData("audio.mp3", "audio/mpeg")]
    [InlineData("video.mp4", "video/mp4")]
    [InlineData("file.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("data.csv", "text/csv")]
    [InlineData("page.html", "text/html")]
    public void GetMimeType_KnownExtension_ReturnsCorrectMimeType(string filePath, string expectedMimeType)
    {
        var result = MimeTypeHelper.GetMimeType(filePath);
        Assert.Equal(expectedMimeType, result);
    }

    [Theory]
    [InlineData("file.xyz")]
    [InlineData("file.unknown")]
    public void GetMimeType_UnknownExtension_ReturnsOctetStream(string filePath)
    {
        var result = MimeTypeHelper.GetMimeType(filePath);
        Assert.Equal("application/octet-stream", result);
    }

    [Fact]
    public void GetMimeType_NoExtension_ReturnsOctetStream()
    {
        var result = MimeTypeHelper.GetMimeType("filenoext");
        Assert.Equal("application/octet-stream", result);
    }

    [Theory]
    [InlineData("FILE.PDF", "application/pdf")]
    [InlineData("Image.PNG", "image/png")]
    [InlineData("doc.Docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public void GetMimeType_CaseInsensitive_ReturnsCorrectMimeType(string filePath, string expectedMimeType)
    {
        var result = MimeTypeHelper.GetMimeType(filePath);
        Assert.Equal(expectedMimeType, result);
    }
}
