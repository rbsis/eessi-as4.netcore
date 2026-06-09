using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Strategies.Retriever;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Eu.EDelivery.AS4.UnitTests.Strategies.Retriever;

/// <summary>
/// Testing <see cref="PayloadRetrieverProvider"/>
/// </summary>
public class GivenPayloadRetrieverProviderFacts
{
    private readonly PayloadRetrieverProvider _sut;

    public GivenPayloadRetrieverProviderFacts()
    {
        _sut = new(
            new FilePayloadRetriever(NullLogger<FilePayloadRetriever>.Instance, StubConfig.Default),
            new TempFilePayloadRetriever(NullLogger<TempFilePayloadRetriever>.Instance),
            new HttpPayloadRetriever(Substitute.For<IRetrieverHttpClient>()));
    }

    [Theory]
    [InlineData(FilePayloadRetriever.Key, typeof(FilePayloadRetriever))]
    [InlineData(HttpPayloadRetriever.Key, typeof(HttpPayloadRetriever))]
    [InlineData(TempFilePayloadRetriever.Key, typeof(TempFilePayloadRetriever))]
    public void CanGetKnownPayloadRetriever(string key, Type expectedRetriever)
    {
        // Arrange
        var payload = new Payload(location: $"{key}{Guid.NewGuid()}");

        // Act
        var actualRetriever = _sut.Get(payload);

        // Assert
        Assert.IsType(expectedRetriever, actualRetriever);
    }


    [Fact]
    public void FailsToGetRetrieverIfNoRetrieverIsRegisteredForType()
    {
        // Arrange
        var payload = new Payload(location: "unknownthing");

        // Act / Assert
        Assert.ThrowsAny<Exception>(() => _sut.Get(payload));
    }
}
