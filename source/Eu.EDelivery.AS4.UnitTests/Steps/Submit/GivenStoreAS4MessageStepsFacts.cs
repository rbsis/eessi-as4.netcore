using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Steps.Submit;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Model;
using Eu.EDelivery.AS4.UnitTests.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Submit;

/// <summary>
/// Testing <see cref="StoreAS4MessageStep" />
/// </summary>
public class GivenStoreAS4MessageStepsFacts : GivenDatastoreFacts
{
    private readonly InMemoryMessageBodyStore _messageBodyStore = new(Default.SerializerProvider);

    [Fact]
    public async Task MessageGetsSavedWithOperationToBeProcessed()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        var sut = new StoreAS4MessageStep(
            Substitute.For<ILogger<StoreAS4MessageStep>>(),
            Default.NewOutMessageService(this, _messageBodyStore));

        // Act
        await sut.ExecuteAsync(
            new MessagingContext(AS4Message.Create(new FilledUserMessage(id)), MessagingContextMode.Submit), CancellationToken.None);

        // Assert
        GetDataStoreContext.AssertOutMessage(
            id,
            async m =>
            {
                Assert.NotNull(m);
                Assert.Equal(Operation.ToBeProcessed, m.Operation);
                Assert.True(await _messageBodyStore.LoadMessageBodyAsync(m.MessageLocation, CancellationToken.None) != Stream.Null);
            });
    }

    protected override void Disposing()
    {
        _messageBodyStore.Dispose();
        base.Disposing();
    }
}
