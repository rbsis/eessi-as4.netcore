using Eu.EDelivery.AS4.TestUtils.Stubs;

namespace Eu.EDelivery.AS4.UnitTests.Repositories;

public class StubMessageBodyRetrieverFacts
{
    [Fact]
    public async Task ReturnsFixedStreamAsync()
    {
        // Arrange
        var expectedStream = Stream.Null;
        var sut = new StubMessageBodyRetriever(() => expectedStream);

        // Act
        var actualStream = await sut.LoadMessageBodyAsync(location: null, cancellation: CancellationToken.None);

        // Assert
        Assert.Equal(expectedStream, actualStream);
    }
}
