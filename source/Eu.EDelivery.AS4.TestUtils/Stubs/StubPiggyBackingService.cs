using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Services;
using Eu.EDelivery.AS4.Strategies.Sender;

namespace Eu.EDelivery.AS4.TestUtils.Stubs;

public class StubPiggyBackingService : IPiggyBackingService
{
    public static readonly StubPiggyBackingService Instance = new();

    private StubPiggyBackingService() { }

    public void InsertRetryForPiggyBackedSignalMessages(IEnumerable<OutMessage> inserts, Model.PMode.RetryReliability reliability)
    {
    }

    public void ResetSignalMessagesToBePiggyBacked(IEnumerable<SignalMessage> signals, SendResult sendResult)
    {
    }

    public Task<IEnumerable<SignalMessage>> SelectToBePiggyBackedSignalMessagesAsync(PullRequest pr, SendingProcessingMode sendingPMode, CancellationToken cancellation)
    {
        return Task.FromResult<IEnumerable<SignalMessage>>([]);
    }
}
