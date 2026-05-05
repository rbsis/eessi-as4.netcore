using System.Linq.Expressions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Repositories;
using Moq;

namespace Eu.EDelivery.AS4.UnitTests.Repositories;

public class GivenAS4MessageStoreProviderFacts
{
    [Fact]
    public void SpyPersisterGetsCalledIfSaveBody()
    {
        TestProviderWithAcceptedPersister(
           sut => sut.SaveAS4Message("ignored location", AS4Message.Empty),
           spy => spy.SaveAS4Message(It.IsAny<string>(), AS4Message.Empty));
    }

    [Fact]
    public void SpyPersisterGetsCalledIfUpdateBody()
    {
        TestProviderWithAcceptedPersister(
            sut => sut.UpdateAS4Message("ignored location", AS4Message.Empty),
            spy => spy.UpdateAS4Message(It.IsAny<string>(), AS4Message.Empty));
    }

    [Fact]
    public async Task ExpectedMessageBodyStores()
    {
        // Arrange
        const string AcceptedString = "find this string";
        var spyStore = Mock.Of<IAS4MessageBodyStore>();
        var sut = new AS4MessageStoreProvider();
        sut.Accept(s => s.Equals(AcceptedString), spyStore);

        // Act
        await sut.LoadMessageBodyAsync(AcceptedString, CancellationToken.None);

        // Assert
        Mock.Get(spyStore).Verify(s => s.LoadMessageBodyAsync(It.IsAny<string>(), CancellationToken.None), Times.Once);
    }

    private static void TestProviderWithAcceptedPersister(
        Action<AS4MessageStoreProvider> act,
        Expression<Action<IAS4MessageBodyStore>> assertion)
    {
        // Arrange
        var spyStore = Mock.Of<IAS4MessageBodyStore>();
        var sut = new AS4MessageStoreProvider();
        sut.Accept(location => true, spyStore);

        // Act
        act(sut);

        // Assert
        Mock.Get(spyStore).Verify(assertion, Times.Once);
    }
}
