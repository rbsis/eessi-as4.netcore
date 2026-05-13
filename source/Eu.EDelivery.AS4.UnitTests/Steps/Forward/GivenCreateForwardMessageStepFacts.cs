using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Steps.Forward;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using MessageExchangePattern = Eu.EDelivery.AS4.Model.PMode.MessageExchangePattern;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Forward;

public class GivenCreateForwardMessageStepFacts : GivenDatastoreFacts
{
    protected readonly InMemoryMessageBodyStore Store = new(Default.SerializerProvider);

    public class GivenValidMessagingContext : GivenCreateForwardMessageStepFacts
    {
        [Fact]
        public async Task ThenMessageIsForwarded()
        {
            // Arrange
            var receivedInMessage = InPersistentUserMessage();
            await InsertInMessageIntoDatastore(receivedInMessage);

            var messagingContext = ContextWithReferencedToBeForwardMessage();

            // Act
            await ExerciseCreateForwardMessage(messagingContext);

            // Assert
            // Verify if there exists a correct OutMessage record.
            using var db = GetDataStoreContext();
            var outMessage = db.OutMessages.First(m => m.EbmsMessageId == receivedInMessage.EbmsMessageId);

            Assert.Equal(Operation.ToBeProcessed, outMessage.Operation);
            Assert.NotNull(messagingContext.SendingPMode);
            Assert.Equal(messagingContext.SendingPMode.MessagePackaging.Mpc, outMessage.Mpc);
            Assert.Equal(messagingContext.SendingPMode.MepBinding.ToString(), outMessage.MEP.ToString());

            var inMessage = db.InMessages.First(m => m.EbmsMessageId == receivedInMessage.EbmsMessageId);

            Assert.Equal(Operation.Forwarded, inMessage.Operation);
        }

        private InMessage InPersistentUserMessage()
        {
            var as4Message = CreateAS4Message(
                new UserMessage("some-message-id", "ref-to-message-id"));

            var location = Store.SaveAS4Message("", as4Message);

            var receivedInMessage = CreateInMessage(as4Message);
            receivedInMessage.MessageLocation = location;

            return receivedInMessage;
        }

        private static AS4Message CreateAS4Message(UserMessage userMessage)
        {
            return AS4Message.Create(userMessage);
        }

        private static InMessage CreateInMessage(AS4Message message)
        {
            var result = new InMessage(message.GetPrimaryMessageId() ?? throw new InvalidOperationException())
            {
                EbmsRefToMessageId = message.PrimaryMessageUnit!.RefToMessageId,
                ContentType = message.ContentType,
                Intermediary = true,
                EbmsMessageType = MessageType.UserMessage,
                Operation = Operation.ToBeForwarded
            };

            result.AssignAS4Properties(message.PrimaryMessageUnit);

            return result;
        }

        private MessagingContext ContextWithReferencedToBeForwardMessage()
        {
            ReceivedEntityMessage receivedMessage;

            using (var db = GetDataStoreContext())
            {
                var inMessage =
                    db.InMessages.First(m => m.Operation == Operation.ToBeForwarded);

                receivedMessage = new ReceivedEntityMessage(inMessage, Stream.Null, "");
            }

            return new MessagingContext(receivedMessage, MessagingContextMode.Forward)
            {
                SendingPMode = CreateSendingPMode()
            };
        }

        private static SendingProcessingMode CreateSendingPMode()
        {
            return new SendingProcessingMode
            {
                Id = "forward-sending-pmode",
                Mep = MessageExchangePattern.OneWay,
                MepBinding = MessageExchangePatternBinding.Pull,
                MessagePackaging = new SendMessagePackaging
                {
                    Mpc = "Some-Modified-Mpc"
                }
            };
        }

        private async Task InsertInMessageIntoDatastore(InMessage receivedInMessage)
        {
            using var db = GetDataStoreContext();
            db.InMessages.Add(receivedInMessage);
            await db.SaveChangesAsync();
        }

        private async Task ExerciseCreateForwardMessage(MessagingContext messagingContext)
        {
            var sut = new CreateForwardMessageStep(
                Substitute.For<ILogger<CreateForwardMessageStep>>(),
                StubConfig.Default,
                Store,
                Default.NewDatastoreRepository(this),
                Default.SerializerProvider);

            await sut.ExecuteAsync(messagingContext, CancellationToken.None);
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    protected override void Disposing()
    {
        Store.Dispose();
    }
}
