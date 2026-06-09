using System.Collections.ObjectModel;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Strategies.Sender;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RetryReliability = Eu.EDelivery.AS4.Entities.RetryReliability;

namespace Eu.EDelivery.AS4.Services;

/// <summary>
/// Service that centralizes the functionality related to the Piggy-Back approach of bundling <see cref="SignalMessage"/>s to <see cref="PullRequest"/>s.
/// </summary>
public class PiggyBackingService : IPiggyBackingService
{
    private readonly ILogger<PiggyBackingService> _logger;
    private readonly IDatastoreRepository _repository;
    private readonly IMarkForRetryService _markForRetryService;
    private readonly IAS4MessageBodyStore _bodyStore;
    private readonly IDbContextFactory<DatastoreContext> _contextFactory;
    private readonly ISerializerProvider _serializerProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PiggyBackingService"/> class.
    /// </summary>
    public PiggyBackingService(
        ILogger<PiggyBackingService> logger,
        IDatastoreRepository repository,
        IMarkForRetryService markForRetryService,
        IAS4MessageBodyStore bodyStore,
        IDbContextFactory<DatastoreContext> contextFactory,
        ISerializerProvider serializerProvider)
    {
        _logger = logger;
        _repository = repository;
        _markForRetryService = markForRetryService;
        _bodyStore = bodyStore;
        _contextFactory = contextFactory;
        _serializerProvider = serializerProvider;
    }

    /// <summary>
    /// Selects the available <see cref="SignalMessage"/>s that are ready to be bundled (PiggyBacked) with the given <see cref="PullRequest"/>.
    /// </summary>
    /// <param name="pr">The <see cref="PullRequest"/> for which a selection of <see cref="SignalMessage"/>s are returned.</param>
    /// <param name="sendingPMode">The sending configuration used to select <see cref="SignalMessage"/>s with the same configuration.</param>
    /// <returns>
    ///     An subsection of the <see cref="SignalMessage"/>s where the referenced send <see cref="UserMessage"/> matches the given <paramref name="pr"/>
    ///     and where the sending configuration given in the <paramref name="sendingPMode"/> matches the stored <see cref="SignalMessage"/> sending configuration.
    /// </returns>
    /// <param name="cancellation"></param>
    public async Task<IEnumerable<SignalMessage>> SelectToBePiggyBackedSignalMessagesAsync(
        PullRequest pr,
        SendingProcessingMode sendingPMode,
        CancellationToken cancellation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sendingPMode.PushConfiguration?.Protocol?.Url);

        var url = sendingPMode.PushConfiguration.Protocol.Url;
        var pullRequestSigned = sendingPMode.Security?.Signing?.IsEnabled == true;

        using var context = await _contextFactory.CreateDbContextAsync(cancellation);

        return await context.TransactionalAsync(async db =>
        {
            var query = db.NativeCommands.SelectToBePiggyBackedSignalMessages(url, pr.Mpc);

            var toBePiggyBackedSignals = new Collection<MessageUnit>();
            foreach (var found in query)
            {
                if (found.MessageLocation == null || found.ContentType == null)
                {
                    continue;
                }

                var body = await _bodyStore.LoadMessageBodyAsync(found.MessageLocation, cancellation);
                var signal = await _serializerProvider
                    .Get(found.ContentType)
                    .DeserializeAsync(body, found.ContentType, cancellation);

                var toBePiggyBacked = signal.SignalMessages.FirstOrDefault(s => s.MessageId == found.EbmsMessageId);
                if (toBePiggyBacked is Receipt || toBePiggyBacked is Error)
                {
                    if (!pullRequestSigned && signal.IsSigned)
                    {
                        _logger.LogWarning("Can't PiggyBack {ToBePiggyBacked} {MessageId} because SignalMessage is signed while the SendingPMode {SendingPModeId} used is not configured for signing",
                            toBePiggyBacked.GetType().Name,
                            toBePiggyBacked.MessageId,
                            sendingPMode.Id);
                    }
                    else
                    {
                        found.Operation = Operation.Sending;
                        toBePiggyBackedSignals.Add(toBePiggyBacked);
                    }
                }
                else if (toBePiggyBacked is not null)
                {
                    _logger.LogWarning("Will not select {ToBePiggyBacked} {MessageId} for PiggyBacking because only Receipts and Errors are allowed SignalMessages to be PiggyBacked with PullRequests",
                        toBePiggyBacked.GetType().Name,
                        toBePiggyBacked.MessageId);
                }
                else
                {
                    _logger.LogWarning("Will not select AS4Message for PiggyBacking because it doesn't contains any Message Units");
                }
            }

            if (toBePiggyBackedSignals.Any())
            {
                await db.SaveChangesAsync();
            }

            return toBePiggyBackedSignals.Cast<SignalMessage>().AsEnumerable();
        },
        cancellation);
    }

    /// <summary>
    /// Resets the PiggyBacked <see cref="SignalMessage"/>s back to its original <see cref="Operation.ToBePiggyBacked"/> state
    /// so it can be picked-up again by the next send-out <see cref="PullRequest"/>.
    /// </summary>
    /// <param name="signals">The <see cref="SignalMessage"/>s that should be resetted for PiggyBacking.</param>
    /// <param name="sendResult">The result of the bundling operation to use when resetting the <see cref="SignalMessage"/>s.</param>
    public void ResetSignalMessagesToBePiggyBacked(IEnumerable<SignalMessage> signals, SendResult sendResult)
    {
        var nonPrSignals = signals
            .Where(s => s is not PullRequest)
            .Select(s => s.MessageId);

        var neverFatalResult = sendResult == SendResult.FatalFail
            ? SendResult.RetryableFail
            : sendResult;

        if (neverFatalResult == SendResult.Success)
        {
            _logger.LogDebug("PiggyBacked SignalMessage(s) was/were correctly send to the sender MSH");
        }
        else if (neverFatalResult == SendResult.RetryableFail)
        {
            _logger.LogDebug("Reset PiggyBacked SignalMessage(s) for the next PullRequest because it was not correctly send to the sender MSH");
        }

        if (nonPrSignals.Any())
        {
            var ids = _repository.GetOutMessageData(m => nonPrSignals.Contains(m.EbmsMessageId), m => m.Id);

            if (ids.Any())
            {
                foreach (var id in ids)
                {
                    _markForRetryService.UpdateAS4MessageForSendResult(id, neverFatalResult);
                }
            }
            else
            {
                _logger.LogWarning(
                    "No stored SignalMessage can be found to reset for PiggyBacking, "
                    + "are you sure that the bundled SignalMessages with PullRequest are stored?");
            }
        }
        else
        {
            _logger.LogDebug("No SignalMessages bundled with PullRequest to reset for PiggyBacking");
        }
    }

    /// <summary>
    /// Mark the stored <see cref="OutMessage"/> for retry/delayed piggy backing.
    /// </summary>
    /// <param name="inserts"></param>
    /// <param name="reliability"></param>
    public void InsertRetryForPiggyBackedSignalMessages(IEnumerable<OutMessage> inserts, Model.PMode.RetryReliability reliability)
    {
        if (reliability?.IsEnabled == true)
        {
            foreach (var m in inserts.Where(i => i.Operation == Operation.ToBePiggyBacked))
            {
                var r = RetryReliability.CreateForOutMessage(
                    refToOutMessageId: m.Id,
                    maxRetryCount: reliability.RetryCount,
                    retryInterval: reliability.RetryInterval.AsTimeSpan(),
                    type: RetryType.PiggyBack);

                _logger.LogDebug(
                    "Insert RetryReliability for ToBePiggyBacked SignalMessage OutMessage {EbmsMessageId} with "
                    + "{{RetryCount={MaxRetryCount}, RetryInterval={RetryInterval}}}",
                    m.EbmsMessageId,
                    r.MaxRetryCount,
                    r.RetryInterval);

                _repository.InsertRetryReliability(r);
            }
        }
        else
        {
            _logger.LogDebug("Will not insert RetryReliability because ReceivingPMode.ReplyHandling.Reliability is not enabeld");
        }
    }
}
