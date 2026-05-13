using Eu.EDelivery.AS4.PayloadService.Infrastructure;
using Eu.EDelivery.AS4.PayloadService.UnitTests.Models;
using Eu.EDelivery.AS4.PayloadService.UnitTests.Serialization;

namespace Eu.EDelivery.AS4.PayloadService.UnitTests.Infrastructure;

/// <summary>
/// Testing <see cref="MultipartPayloadReader"/>
/// </summary>
public class GivenMultipartPayloadReaderFacts
{
    [Fact]
    public void CannotCreateReader_IfContentTypeIsntMultiPart()
    {
        Assert.False(MultipartPayloadReader.TryCreate(Stream.Null, "application/json").success);
    }

    [Fact]
    public async Task ReadsExpectedContent()
    {
        // Arrange
        const string ExpectedContent = "message data!";
        using var actualStream = new MemoryStream();
        var reader = await CreateStubReaderThatReturns(ExpectedContent, actualStream);
        var waitHandle = new ManualResetEvent(initialState: false);

        // Act
        await reader.StartReading(payload =>
        {
            // Assert
            Assert.Equal(ExpectedContent, payload.DeserializeContent());

            waitHandle.Set();
            return Task.CompletedTask;
        });

        // Assert
        Assert.True(waitHandle.WaitOne(timeout: TimeSpan.FromSeconds(1)));
    }

    private static async Task<MultipartPayloadReader> CreateStubReaderThatReturns(string expectedContent, Stream actualStream)
    {
        using var contentStream = expectedContent.AsStream();
        var multipartContent = new MultipartFormDataContent { { new StreamContent(contentStream), "name", "filename" } };
        await multipartContent.CopyToAsync(actualStream);
        actualStream.Position = 0;

        return MultipartPayloadReader.TryCreate(actualStream, multipartContent.Headers.ContentType?.ToString())
            .reader ?? throw new NullReferenceException();
    }
}
