using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Strategies.Sender;

namespace Eu.EDelivery.AS4.Services;

public interface IPiggyBackingService
{
    void InsertRetryForPiggyBackedSignalMessages(IEnumerable<OutMessage> inserts, Model.PMode.RetryReliability reliability);
    void ResetSignalMessagesToBePiggyBacked(IEnumerable<SignalMessage> signals, SendResult sendResult);
    Task<IEnumerable<SignalMessage>> SelectToBePiggyBackedSignalMessagesAsync(PullRequest pr, SendingProcessingMode sendingPMode, CancellationToken cancellation);
}
