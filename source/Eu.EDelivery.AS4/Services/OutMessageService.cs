using System.Collections.ObjectModel;
using Eu.EDelivery.AS4.Builders.Entities;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;
using Microsoft.Extensions.Logging;
using MessageExchangePattern = Eu.EDelivery.AS4.Entities.MessageExchangePattern;
using ReceptionAwareness = Eu.EDelivery.AS4.Model.PMode.ReceptionAwareness;

namespace Eu.EDelivery.AS4.Services;

/// <summary>
/// Service to expose db operations related to messages that needs to be send out, 
/// either directly via the Send Agent or via the Outbound Processing Agent.
/// </summary>
public class OutMessageService : IOutMessageService
{
    private readonly ILogger<OutMessageService> _logger;
    private readonly IDatastoreRepository _repository;
    private readonly IAS4MessageBodyStore _messageBodyStore;
    private readonly IConfig _configuration;
    private readonly ISerializerProvider _serializerProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutMessageService" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="config">The configuration used to retrieve the response <see cref="SendingProcessingMode"/> while inserting messages and the store location for <see cref="OutMessage"/>s.</param>
    /// <param name="repository">The repository used to insert and update <see cref="OutMessage"/>s.</param>
    /// <param name="messageBodyStore">The <see cref="IAS4MessageBodyStore"/> that must be used to persist the AS4 Message Body.</param>
    /// <param name="serializerProvider"></param>
    public OutMessageService(
        ILogger<OutMessageService> logger,
        IConfig config,
        IDatastoreRepository repository,
        IAS4MessageBodyStore messageBodyStore,
        ISerializerProvider serializerProvider)
    {
        _logger = logger;
        _configuration = config;
        _repository = repository;
        _messageBodyStore = messageBodyStore;
        _serializerProvider = serializerProvider;
    }

    /// <summary>
    /// Gets the non-intermediary stored <see cref="AS4Message"/>s matching the specified ebMS <paramref name="messageIds"/>.
    /// </summary>
    /// <param name="messageIds">The ebMS message identifiers.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<IEnumerable<AS4Message>> GetNonIntermediaryAS4UserMessagesForIds(IEnumerable<string> messageIds, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(messageIds);

        if (!messageIds.Any())
        {
            _logger.LogTrace("Specified ebMS message identifiers is empty");
            return [];
        }

        var messages =
            _repository.GetOutMessageData(
                           m => messageIds.Contains(m.EbmsMessageId)
                                && !m.Intermediary,
                           m => m)
                       .Where(m => m != null);

        if (!messages.Any())
        {
            return [];
        }

        var foundMessages = new Collection<AS4Message>();
        foreach (var m in messages)
        {
            if (m.MessageLocation == null || m.ContentType == null)
            {
                continue;
            }

            var body = await _messageBodyStore.LoadMessageBodyAsync(m.MessageLocation, cancellation);
            var foundMessage = await _serializerProvider
                .Get(m.ContentType)
                .DeserializeAsync(body, m.ContentType, cancellation);

            foundMessages.Add(foundMessage);
        }

        return foundMessages.AsEnumerable();
    }

    /// <summary>
    /// Inserts all the message units of the specified <paramref name="as4Message"/> as <see cref="OutMessage"/> records 
    /// each containing the appropriate Status and Operation.
    /// User messages will be set to <see cref="Operation.ToBeProcessed"/>
    /// Signal messages that must be async returned will be set to <see cref="Operation.ToBeSent"/>.
    /// </summary>
    /// <param name="as4Message">The message for which the containing message units will be inserted.</param>
    /// <param name="sendingPMode">The processing mode that will be stored with each message unit if present.</param>
    /// <param name="receivingPMode">The processing mode that will be used to determine if the signal messages must be async returned, this pmode will be stored together with the message units.</param>
    public IEnumerable<OutMessage> InsertAS4Message(
        AS4Message as4Message,
        SendingProcessingMode? sendingPMode,
        ReceivingProcessingMode? receivingPMode)
    {
        if (!as4Message.MessageUnits.Any())
        {
            _logger.LogTrace("Incoming AS4Message hasn't got any message units to insert");
            return [];
        }

        var messageBodyLocation = _messageBodyStore.SaveAS4Message(
            _configuration.OutMessageStoreLocation,
            as4Message);

        var relatedInMessageMeps = GetEbsmsMessageIdsOfRelatedSignals(as4Message);

        var results = new Collection<OutMessage>();
        foreach (var messageUnit in as4Message.MessageUnits)
        {
            var pmode = SendingOrReceivingPMode(messageUnit, sendingPMode, receivingPMode);

            (var st, var op) = DetermineReplyPattern(messageUnit, relatedInMessageMeps, receivingPMode);

            var url = SelectCorrectUrlForOutgoingMessage(messageUnit, sendingPMode, receivingPMode);

            var outMessage = OutMessageBuilder
                .ForMessageUnit(messageUnit, as4Message.ContentType, pmode)
                .BuildForSending(messageBodyLocation, url, st, op);

            _logger.LogDebug("Insert OutMessage {EbmsMessageType} with {{Operation={Operation}, Status={Status}}}",
                outMessage.EbmsMessageType,
                outMessage.Operation,
                outMessage.Status);
            _repository.InsertOutMessage(outMessage);
            results.Add(outMessage);
        }

        return results.AsEnumerable();
    }

    private IDictionary<string, MessageExchangePattern> GetEbsmsMessageIdsOfRelatedSignals(AS4Message as4Message)
    {
        var signalMessageIds = as4Message.SignalMessages
            .Where(s => s.RefToMessageId != null)
            .Select(s => s.RefToMessageId!)
            .Distinct();

        return _repository
            .GetInMessagesData(signalMessageIds, m => new { m.EbmsMessageId, m.MEP })
            .Distinct()
            .ToDictionary(r => r.EbmsMessageId, r => r.MEP);
    }

    private static (OutStatus, Operation) DetermineReplyPattern(
        MessageUnit mu,
        IDictionary<string, MessageExchangePattern> relatedInMessageMeps,
        ReceivingProcessingMode? receivingPMode)
    {
        if (mu is UserMessage)
        {
            return (OutStatus.NotApplicable, Operation.ToBeProcessed);
        }

        var key = mu.RefToMessageId ?? string.Empty;
        if (!relatedInMessageMeps.TryGetValue(key, out var value))
        {
            return (OutStatus.Created, Operation.NotApplicable);
        }

        var replyPattern = receivingPMode?.ReplyHandling?.ReplyPattern ?? ReplyHandling.DefaultReplyPattern;

        var userMessageWasSendViaPull = value == MessageExchangePattern.Pull;
        if (userMessageWasSendViaPull
            && replyPattern == ReplyPattern.Response)
        {
            throw new InvalidOperationException(
                $"Cannot determine Status and Operation because ReceivingPMode {receivingPMode?.Id} ReplyHandling.ReplyPattern = Response "
                + "while the UserMessage has been send via pulling. Please change the ReplyPattern to 'CallBack' or 'PiggyBack'");
        }

        var signalShouldBePiggyBackedToPullRequest = replyPattern == ReplyPattern.PiggyBack;
        if (userMessageWasSendViaPull
            && signalShouldBePiggyBackedToPullRequest)
        {
            return (OutStatus.Created, Operation.ToBePiggyBacked);
        }

        var signalShouldBeRespondedAsync = replyPattern == ReplyPattern.Callback;
        if (signalShouldBeRespondedAsync)
        {
            return (OutStatus.Created, Operation.ToBeSent);
        }

        return (OutStatus.Sent, Operation.NotApplicable);
    }

    private IPMode? SendingOrReceivingPMode(
        MessageUnit mu,
        SendingProcessingMode? sendPMode,
        ReceivingProcessingMode? receivePMode)
    {
        if (mu is UserMessage && sendPMode != null)
        {
            _logger.LogTrace("Use SendingPMode {PModeId} to insert with the UserMessage {MessageId}", sendPMode.Id, mu.MessageId);
            return sendPMode;
        }

        if (mu is SignalMessage && receivePMode != null)
        {
            // All SignalMessages that are OutMessages are responding messages, which require the ReceivingPMode
            _logger.LogTrace("Use ReceivingPMode {PModeId} to insert with the SignalMessage {MessageId}", receivePMode.Id, mu.MessageId);
            return receivePMode;
        }

        // When no PMode was determined for the message, we can't insert any PMode with the message.
        return null;
    }

    private static string? SelectCorrectUrlForOutgoingMessage(
        MessageUnit mu,
        SendingProcessingMode? sendingPMode,
        ReceivingProcessingMode? receivingPMode)
    {
        if (mu is UserMessage
            && sendingPMode != null
            && sendingPMode.MepBinding == MessageExchangePatternBinding.Push)
        {
            return sendingPMode.PushConfiguration?.Protocol?.Url;
        }

        if (mu is SignalMessage && receivingPMode != null)
        {
            if (receivingPMode.ReplyHandling?.ReplyPattern == ReplyPattern.PiggyBack
                && sendingPMode != null)
            {
                // Only when we piggy back a SignalMessage in a Pull Receive scenario
                // we need the URL of the SendingPMode that is used to send PullRequests.
                return sendingPMode.PushConfiguration?.Protocol?.Url;
            }

            // Every other SignalMessage that gets responded will have the URL of the ReceivingPMode
            return receivingPMode.ReplyHandling?.ResponseConfiguration?.Protocol?.Url;
        }

        return null;
    }

    /// <summary>
    /// Updates a <see cref="AS4Message"/> by marking it as ready for sending.
    /// </summary>
    /// <param name="outMessageId">The Id that uniquely identifies the OutMessage record in the database.</param>
    /// <param name="message">The message to be sent.</param>
    /// <param name="awareness">The reliability reception awareness used during the sending of the message</param>
    public void UpdateAS4MessageToBeSent(
        long outMessageId,
        AS4Message message,
        ReceptionAwareness? awareness)
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageBodyLocation = _repository.GetOutMessageData(outMessageId, m => m.MessageLocation)
            ?? throw new InvalidOperationException("Messagebody location cannot be null");

        _messageBodyStore.UpdateAS4Message(messageBodyLocation, message);

        var messageMustBeForwarded = _repository.GetOutMessageData(outMessageId, m => m.Intermediary);

        _repository.UpdateOutMessage(
            outMessageId,
            m =>
            {
                m.Operation = Operation.ToBeSent;
                m.MessageLocation = messageBodyLocation;
                _logger.LogDebug("Update {EbmsMessageType} OutMessage {EbmsMessageId} with {{Operation=ToBeSent}}", m.EbmsMessageType, m.EbmsMessageId);

                if (awareness?.IsEnabled ?? false)
                {
                    // When a multihop message is received by an i-MSH, that message must be forwarded to another MSH.
                    // (Send) RetryReliability should not be enabled for this message however; even if this is configured in the SendingPMode.
                    if (messageMustBeForwarded)
                    {
                        _logger.LogWarning("SendingPMode.Reliability.ReceptionAwareness.IsEnabled = true but the incoming message is a multihop message and must be forwarded");
                    }
                    else
                    {
                        var r = Entities.RetryReliability.CreateForOutMessage(
                            outMessageId,
                            awareness.RetryCount,
                            awareness.RetryInterval.AsTimeSpan(),
                            RetryType.Send);

                        _logger.LogTrace(
                            "Insert RetryReliability for OutMessage {EbmsMessageId} with {{RetryCount={RetryCount}, RetryInterval={RetryInterval}}}",
                            m.EbmsMessageId,
                            awareness.RetryCount,
                            awareness.RetryInterval);

                        _repository.InsertRetryReliability(r);
                    }
                }
                else
                {
                    _logger.LogTrace("Will not insert RetryReliability record for reception awareness since the SendingPMode.Reliability.ReceptionAwareness.IsEnabled = false");
                }
            });
    }
}
