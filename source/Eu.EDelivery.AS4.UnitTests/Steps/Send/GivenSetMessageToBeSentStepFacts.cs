using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Steps.Send;
using Eu.EDelivery.AS4.TestUtils.Repositories;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Model;
using Eu.EDelivery.AS4.UnitTests.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Send;

public class GivenSetMessageToBeSentStepFacts : GivenDatastoreFacts
{
    private readonly InMemoryMessageBodyStore _messageBodyStore = new(Default.SerializerProvider);

    [Fact]
    public async Task StepSetsMessageToBeSent()
    {
        // Assert
        string messageId = Guid.NewGuid().ToString(),
               expected = Guid.NewGuid().ToString();

        var sut = new SetMessageToBeSentStep(
            Substitute.For<ILogger<SetMessageToBeSentStep>>(),
            Default.NewOutMessageService(this, _messageBodyStore));

        var messagingContext = SetupMessagingContext(messageId, Operation.Processing, expected);

        // Act
        await sut.ExecuteAsync(messagingContext, CancellationToken.None);

        // Assert
        GetDataStoreContext.AssertOutMessage(
            messageId,
            m =>
            {
                Assert.NotNull(m);
                Assert.Equal(expected, m.MessageLocation);
                Assert.Equal(Operation.ToBeSent, m.Operation);
            });
    }

    private MessagingContext SetupMessagingContext(string ebmsMessageId, Operation operation, string messageLocation)
    {
        var outMessage = new OutMessage(ebmsMessageId: ebmsMessageId)
        {
            MessageLocation = messageLocation,
            Operation = operation
        };

        var insertedOutMessage = GetDataStoreContext.InsertOutMessage(outMessage, withReceptionAwareness: false);

        Assert.NotEqual(default, insertedOutMessage.Id);

        var receivedMessage = new ReceivedEntityMessage(insertedOutMessage);

        return new MessagingContext(
            AS4Message.Create(new FilledUserMessage(ebmsMessageId)),
            receivedMessage,
            MessagingContextMode.Send);
    }

    protected override void Disposing()
    {
        _messageBodyStore.Dispose();
        base.Disposing();
    }
}
