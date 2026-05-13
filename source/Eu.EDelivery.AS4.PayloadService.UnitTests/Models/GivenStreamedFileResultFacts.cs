using System.Text;
using Eu.EDelivery.AS4.PayloadService.Models;

namespace Eu.EDelivery.AS4.PayloadService.UnitTests.Models;

public class GivenStreamedFileResultFacts
{
    [Fact]
    public void ReturnsExpectedResult()
    {
        const string ExpectedContent = "message data!";
        using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(ExpectedContent));
        var streamedFileResult = new StreamedFileResult(contentStream, "download-filename", "content-type");

        StreamedFileResultAssert.OnContent(
            streamedFileResult: streamedFileResult,
            assertion: actualContent => Assert.Equal(ExpectedContent, actualContent));
    }
}
