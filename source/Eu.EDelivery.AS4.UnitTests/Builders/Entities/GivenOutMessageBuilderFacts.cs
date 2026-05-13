using Eu.EDelivery.AS4.Builders.Entities;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Serialization;

namespace Eu.EDelivery.AS4.UnitTests.Builders.Entities;

/// <summary>
/// Testing <see cref="OutMessageBuilder" />
/// </summary>
public class GivenOutMessageBuilderFacts
{
    public class GivenValidArguments : GivenOutMessageBuilderFacts
    {
        [Fact]
        public async Task ThenBuildOutMessageSucceedsWithAS4Message()
        {
            // Arrange
            var as4Message = CreateAS4MessageWithUserMessage(Guid.NewGuid().ToString());

            // Act
            var outMessage = BuildForPrimaryMessageUnit(as4Message);

            // Assert
            Assert.NotNull(outMessage);
            Assert.Equal(as4Message.ContentType, outMessage.ContentType);
            Assert.Equal(MessageType.UserMessage, outMessage.EbmsMessageType);
            Assert.Equal(await AS4XmlSerializer.ToStringAsync(ExpectedPMode(), CancellationToken.None), outMessage.PMode);
        }

        [Fact]
        public void ThenBuildOutMessageSucceedsWithAS4MessageAndEbmsMessageId()
        {
            // Arrange
            var messageId = Guid.NewGuid().ToString();
            var as4Message = CreateAS4MessageWithUserMessage(messageId);


            // Act
            var outMessage = BuildForPrimaryMessageUnit(as4Message);

            // Assert
            Assert.Equal(messageId, outMessage.EbmsMessageId);
        }

        [Fact]
        public void ThenBuildOutMessageSucceedsForReceiptMessage()
        {
            // Arrange
            var messageId = Guid.NewGuid().ToString();
            var as4Message = AS4Message.Create(
                new Receipt(messageId, $"reftoid-{Guid.NewGuid()}"),
                ExpectedPMode());

            // Act
            var outMessage = BuildForPrimaryMessageUnit(as4Message);

            // Assert
            Assert.Equal(messageId, outMessage.EbmsMessageId);
            Assert.Equal(MessageType.Receipt, outMessage.EbmsMessageType);
        }

        [Fact]
        public void ThenBuildOutMessageSucceedsForErrorMessage()
        {
            // Arrange
            var messageId = Guid.NewGuid().ToString();
            var as4Message = AS4Message.Create(new Error(messageId, $"user-{Guid.NewGuid()}"), ExpectedPMode());

            // Act
            var outMessage = BuildForPrimaryMessageUnit(as4Message);

            // Assert
            Assert.Equal(messageId, outMessage.EbmsMessageId);
            Assert.Equal(MessageType.Error, outMessage.EbmsMessageType);
        }

        private static OutMessage BuildForPrimaryMessageUnit(AS4Message m)
        {
            return OutMessageBuilder
                .ForMessageUnit(m.PrimaryMessageUnit, m.ContentType, ExpectedPMode())
                .BuildForSending("message-location", "message-url", OutStatus.NotApplicable, Operation.NotApplicable);
        }
    }

    protected static SendingProcessingMode ExpectedPMode()
    {
        return new SendingProcessingMode { Id = "pmode-id" };
    }

    protected static AS4Message CreateAS4MessageWithUserMessage(string messageId)
    {
        return AS4Message.Create(new UserMessage(messageId), ExpectedPMode());
    }
}
