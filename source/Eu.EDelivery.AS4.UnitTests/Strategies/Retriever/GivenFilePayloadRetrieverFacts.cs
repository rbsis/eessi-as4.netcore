using Eu.EDelivery.AS4.Strategies.Retriever;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.UnitTests.Strategies.Retriever;

public class GivenFilePayloadRetrieverFacts : PseudoConfig
{
    [Fact]
    public async Task RetrievePayloadFailsWithInvalidFilePath()
    {
        await Assert.ThrowsAnyAsync<Exception>(
            () => ExerciseRetrieving("invalid-location"));
    }

    [Theory]
    [InlineData(@"config\settings.xml")]
    [InlineData(@"config\send-pmodes\pmode.xml")]
    [InlineData(@"config\receive-pmodes\pmode.xml")]
    public async Task RetrievePayloadFailsWithTraversalFilePath(string location)
    {
        await Assert.ThrowsAsync<NotSupportedException>(
            () => ExerciseRetrieving(location));
    }

    private async Task<Stream> ExerciseRetrieving(string location)
    {
        var sut = new FilePayloadRetriever(NullLogger<FilePayloadRetriever>.Instance, configuration: this);
        return await sut.RetrievePayloadAsync(location, CancellationToken.None);
    }

    /// <summary>
    /// Gets the location where the payloads should be retrieved.
    /// </summary>
    public override string PayloadRetrievalLocation => @"\messages\attachments";
}
