using Eu.EDelivery.AS4.PayloadService.Models;
using Eu.EDelivery.AS4.PayloadService.Persistance;
using Eu.EDelivery.AS4.PayloadService.UnitTests.Models;
using Eu.EDelivery.AS4.PayloadService.UnitTests.Serialization;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.PayloadService.UnitTests.Persistance;

/// <summary>
/// Testing <see cref="FilePayloadPersister"/>
/// </summary>
[DeletePayloads("GivenFilePayloadPersisterFacts")]
public class GivenFilePayloadPersisterFacts
{
    private readonly IPayloadPersister _persister = new FilePayloadPersister(
        NullLogger<FilePayloadPersister>.Instance,
        new CurrentDirectoryHostEnvironment { ContentRootPath = Path.Combine(Directory.GetCurrentDirectory(), "GivenFilePayloadPersisterFacts") });

    [Fact]
    public async Task WritesFileWithMetaToDisk()
    {
        // Arrange
        const string ExpectedContent = "message data!";
        using var serializeContent = ExpectedContent.AsStream();

        // Act
        var payload = new Payload(serializeContent, CreateUniquePayloadMeta());
        var newPayloadId = await _persister.SavePayload(payload);

        // Assert
        Assert.Equal(ExpectedContent, DeserializeContent(newPayloadId));
        Assert.Contains("originalfilename:", DeserializeContent(newPayloadId + ".meta"));
    }

    [Fact]
    public async Task LoadsPayloadWithMetaFromDisk()
    {
        // Arrange
        const string ExpectedContent = "message data!";
        using var serializeContent = ExpectedContent.AsStream();

        // Act
        var payload = new Payload(serializeContent, CreateUniquePayloadMeta());
        var savedPayloadId = await _persister.SavePayload(payload);
        using var actualPayload = await _persister.LoadPayload(savedPayloadId);

        // Assert
        Assert.Equal(ExpectedContent, actualPayload.DeserializeContent());
    }

    private static PayloadMeta CreateUniquePayloadMeta()
    {
        return new PayloadMeta(Guid.NewGuid() + ".txt");
    }

    private static string DeserializeContent(string id)
    {
        return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "GivenFilePayloadPersisterFacts", "Payloads", id));
    }
}
