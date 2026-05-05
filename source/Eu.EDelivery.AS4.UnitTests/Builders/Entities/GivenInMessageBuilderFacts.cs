using Eu.EDelivery.AS4.Builders.Entities;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Serialization;
using MessageExchangePattern = Eu.EDelivery.AS4.Entities.MessageExchangePattern;

namespace Eu.EDelivery.AS4.UnitTests.Builders.Entities;

/// <summary>
/// Testing <see cref="InMessageBuilder" />
/// </summary>
public class GivenInMessageBuilderFacts
{
    public class GivenValidArguments : GivenInMessageBuilderFacts
    {
        [Fact]
        public async Task ThenBuildInMessageSucceedsWithAS4MessageAndMessageUnit()
        {
            // Arrange
            var as4Message = AS4Message.Empty;
            var receipt = CreateReceiptMessageUnit();

            // Act
            var inMessage =
                InMessageBuilder.ForSignalMessage(receipt, as4Message, MessageExchangePattern.Push)
                                .WithPMode(new SendingProcessingMode())
                                .BuildAsToBeProcessed();

            // Assert
            Assert.NotNull(inMessage);
            Assert.Equal(as4Message.ContentType, inMessage.ContentType);
            Assert.Equal(await AS4XmlSerializer.ToStringAsync(new SendingProcessingMode(), CancellationToken.None), inMessage.PMode);
            Assert.Equal(MessageType.Receipt, inMessage.EbmsMessageType);
        }
    }

    protected static Receipt CreateReceiptMessageUnit()
    {
        return new Receipt($"receipt-{Guid.NewGuid()}", $"user-{Guid.NewGuid()}");
    }
}
