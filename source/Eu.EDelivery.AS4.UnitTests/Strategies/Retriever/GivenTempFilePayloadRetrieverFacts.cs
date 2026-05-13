using Eu.EDelivery.AS4.Strategies.Retriever;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.UnitTests.Strategies.Retriever;

public class GivenTempFilePayloadRetrieverFacts
{
    [Fact]
    public async Task TemporaryFileGetsDeletedAfterBeingRetrieved()
    {
        // Arrange
        var fixture = Path.GetTempFileName();
        var sut = new TempFilePayloadRetriever(NullLogger<TempFilePayloadRetriever>.Instance);

        // Act
        await (await sut.RetrievePayloadAsync(fixture, CancellationToken.None)).DisposeAsync();

        // Assert
        Assert.False(File.Exists(fixture), "Temporary file isn't deleted afterwards");
    }
}
