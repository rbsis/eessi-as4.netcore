using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;

namespace Eu.EDelivery.AS4.Services;

public interface IOutMessageService
{
    Task<IEnumerable<AS4Message>> GetNonIntermediaryAS4UserMessagesForIds(IEnumerable<string> messageIds, CancellationToken cancellation);
    IEnumerable<OutMessage> InsertAS4Message(AS4Message as4Message, SendingProcessingMode? sendingPMode, ReceivingProcessingMode? receivingPMode);
    void UpdateAS4MessageToBeSent(long outMessageId, AS4Message message, ReceptionAwareness? awareness);
}
