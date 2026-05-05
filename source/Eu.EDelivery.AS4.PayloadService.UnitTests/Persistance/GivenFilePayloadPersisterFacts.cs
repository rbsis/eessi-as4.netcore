using Eu.EDelivery.AS4.PayloadService.Models;
using Eu.EDelivery.AS4.PayloadService.Persistance;
using Eu.EDelivery.AS4.PayloadService.UnitTests.Models;
using Eu.EDelivery.AS4.PayloadService.UnitTests.Serialization;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.PayloadService.UnitTests.Persistance;

/// <summary>
/// Testing <see cref="FilePayloadPersister"/>
/// </summary>
[DeletePayloads]
public class GivenFilePayloadPersisterFacts
{
    [Fact]
    [DeletePayloads]
    public async Task WritesFileWithMetaToDisk()
    {
        // Arrange
        const string ExpectedContent = "message data!";
        using var serializeContent = ExpectedContent.AsStream();
        var persister = new FilePayloadPersister(
            NullLogger<FilePayloadPersister>.Instance,
            new CurrentDirectoryHostEnvironment());

        // Act
        var payload = new Payload(serializeContent, CreateUniquePayloadMeta());
        var newPayloadId = await persister.SavePayload(payload);

        // Assert
        Assert.Equal(ExpectedContent, DeserializeContent(newPayloadId));
        Assert.Contains("originalfilename:", DeserializeContent(newPayloadId + ".meta"));
    }

    [Fact]
    [DeletePayloads]
    public async Task LoadsPayloadWithMetaFromDisk()
    {
        // Arrange
        const string ExpectedContent = "message data!";
        using var serializeContent = ExpectedContent.AsStream();
        var persister = new FilePayloadPersister(
            NullLogger<FilePayloadPersister>.Instance,
            new CurrentDirectoryHostEnvironment());

        // Act
        var payload = new Payload(serializeContent, CreateUniquePayloadMeta());
        var savedPayloadId = await persister.SavePayload(payload);
        using var actualPayload = await persister.LoadPayload(savedPayloadId);

        // Assert
        Assert.Equal(ExpectedContent, actualPayload.DeserializeContent());
    }

    private static PayloadMeta CreateUniquePayloadMeta()
    {
        return new PayloadMeta(Guid.NewGuid() + ".txt");
    }

    private static string DeserializeContent(string id)
    {
        return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Payloads", id));
    }
}
