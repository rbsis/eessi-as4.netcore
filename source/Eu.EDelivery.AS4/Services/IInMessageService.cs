using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;

namespace Eu.EDelivery.AS4.Services;

public interface IInMessageService
{
    Task<AS4Message> InsertAS4MessageAsync(AS4Message as4Message, ReceivedMessage originalMessage, SendingProcessingMode? sendingPMode, Entities.MessageExchangePattern mep, CancellationToken cancellation);
    void InsertDeadLetteredErrorForAsync(string ebmsMessageId, Entities.MessageExchangePattern mep, SendingProcessingMode? sendingPMode);
    void UpdateAS4MessageForMessageHandling(AS4Message as4Message, SendingProcessingMode? sendingPMode, ReceivingProcessingMode? receivingPMode);
}
