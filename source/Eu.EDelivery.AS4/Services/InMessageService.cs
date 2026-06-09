using System.Data;
using System.Text;
using Eu.EDelivery.AS4.Builders.Entities;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Streaming;
using Microsoft.Extensions.Logging;
using MessageExchangePattern = Eu.EDelivery.AS4.Entities.MessageExchangePattern;
using RetryReliability = Eu.EDelivery.AS4.Model.PMode.RetryReliability;

namespace Eu.EDelivery.AS4.Services;

/// <summary>
/// Repository to expose Data store related operations
/// for the Update Data store Steps
/// </summary>
public class InMessageService : IInMessageService
{
    private readonly ILogger<InMessageService> _logger;
    private readonly IConfig _configuration;

    private readonly IDatastoreRepository _repository;
    private readonly IExceptionService _exceptionService;
    private readonly IIdentifierFactory _identifierFactory;
    private readonly IAS4MessageBodyStore _bodyStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMessageService"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="config">The configuration.</param>
    /// <param name="repository">The repository.</param>
    /// <param name="exceptionService"></param>
    /// <param name="identifierFactory"></param>
    /// <param name="messageBodyStore"></param>
    public InMessageService(
        ILogger<InMessageService> logger,
        IConfig config,
        IDatastoreRepository repository,
        IExceptionService exceptionService,
        IIdentifierFactory identifierFactory,
        IAS4MessageBodyStore messageBodyStore)
    {
        _logger = logger;
        _configuration = config;
        _repository = repository;
        _exceptionService = exceptionService;
        _identifierFactory = identifierFactory;
        _bodyStore = messageBodyStore;
    }

    /// <summary>
    /// Insert a DeadLettered AS4 Error refering a specified <paramref name="ebmsMessageId"/> 
    /// for a specified <paramref name="mep"/> notifying only if the specified <paramref name="sendingPMode"/> is configured this way.
    /// </summary>
    /// <param name="ebmsMessageId"></param>
    /// <param name="mep"></param>
    /// <param name="sendingPMode"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public void InsertDeadLetteredErrorForAsync(
        string ebmsMessageId,
        MessageExchangePattern mep,
        SendingProcessingMode? sendingPMode)
    {
        ArgumentNullException.ThrowIfNull(ebmsMessageId);

        var errorMessage =
            Error.FromErrorResult(
                _identifierFactory.Create(),
                ebmsMessageId,
                new ErrorResult("Missing Receipt", ErrorAlias.MissingReceipt));

        var as4Message = AS4Message.Create(errorMessage, sendingPMode);

        // We do not use the InMessageService to persist the incoming message here, since this is not really
        // an incoming message.  We create this InMessage in order to be able to notify the Message Producer
        // if he should be notified when a message cannot be sent.
        // (Maybe we should only create the InMessage when notification is enabled ?)
        var location = _bodyStore.SaveAS4Message(
            location: _configuration.InMessageStoreLocation,
            message: as4Message);

        var inMessage = InMessageBuilder
            .ForSignalMessage(errorMessage, as4Message, mep)
            .WithPMode(sendingPMode)
            .OnLocation(location)
            .BuildAsDeadLetteredError();

        _logger.LogDebug("Create Error for missed Receipt with {{Operation={Operation}}}", inMessage.Operation);
        _repository.InsertInMessage(inMessage);
    }

    /// <summary>
    /// Inserts a received Message in the DataStore.
    /// For each message-unit that exists in the AS4Message,an InMessage record is created.
    /// The AS4 Message Body is persisted as it has been received.
    /// </summary>
    /// <remarks>The received Message is parsed to an AS4 Message instance.</remarks>
    /// <param name="sendingPMode"></param>
    /// <param name="mep"></param>
    /// <param name="cancellation"></param>
    /// <param name="as4Message"></param>
    /// <param name="originalMessage"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    /// <returns>A MessagingContext instance that contains the parsed AS4 Message.</returns>
    public async Task<AS4Message> InsertAS4MessageAsync(
        AS4Message as4Message,
        ReceivedMessage originalMessage,
        SendingProcessingMode? sendingPMode,
        MessageExchangePattern mep,
        CancellationToken cancellation)
    {
        if (originalMessage == null)
        {
            throw new InvalidOperationException("The MessagingContext must contain a ReceivedMessage");
        }

        // TODO: should we start the transaction here.
        var location =
            await _bodyStore.SaveAS4MessageStreamAsync(
                location: _configuration.InMessageStoreLocation,
                as4MessageStream: originalMessage.UnderlyingStream, cancellation: cancellation);

        originalMessage.UnderlyingStream.MovePositionToStreamStart();

        try
        {
            InsertUserMessages(as4Message, mep, location, sendingPMode);
            InsertSignalMessages(as4Message, mep, location, sendingPMode);

            return as4Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Insert messages failed");

            await _exceptionService.InsertIncomingExceptionAsync(ex, new MemoryStream(Encoding.UTF8.GetBytes(location)), cancellation);

            throw;
        }
    }

    private void InsertUserMessages(
        AS4Message as4Message,
        MessageExchangePattern mep,
        string location,
        SendingProcessingMode? pmode)
    {
        if (!as4Message.HasUserMessage)
        {
            _logger.LogTrace("No UserMessages present to be inserted");
            return;
        }

        var duplicateUserMessages =
            DetermineDuplicateUserMessageIds(as4Message.UserMessages.Select(m => m.MessageId));

        foreach (var userMessage in as4Message.UserMessages)
        {
            if (userMessage.IsTest)
            {
                _logger.LogTrace("Incoming UserMessage {MessageId} is a 'Test Message'", userMessage.MessageId);
            }

            userMessage.IsDuplicate = IsUserMessageDuplicate(userMessage, duplicateUserMessages);

            try
            {
                var inMessage = InMessageBuilder
                    .ForUserMessage(userMessage, as4Message, mep)
                    .WithPMode(pmode)
                    .OnLocation(location)
                    .BuildAsToBeProcessed();

                _logger.LogDebug(
                    "Insert InMessage UserMessage {MessageId} with {{Operation={Operation}, Status={Status}, PModeId={PModeId}, IsTest={IsTest}, IsDuplicate={IsDuplicate}}}",
                    userMessage.MessageId,
                    inMessage.Operation,
                    inMessage.Status,
                    pmode?.Id ?? "null",
                    userMessage.IsTest,
                    userMessage.IsDuplicate);

                _repository.InsertInMessage(inMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to insert UserMessage {MessageId}", userMessage.MessageId);

                throw new DataException($"Unable to insert UserMessage {userMessage.MessageId}", ex);
            }
        }
    }

    private IDictionary<string, bool> DetermineDuplicateUserMessageIds(IEnumerable<string> searchedMessageIds)
    {
        var duplicateMessageIds = _repository.SelectExistingInMessageIds(searchedMessageIds);

        return MergeTwoListsIntoADuplicateMessageMapping(searchedMessageIds, duplicateMessageIds);
    }

    private void InsertSignalMessages(
        AS4Message as4Message,
        MessageExchangePattern mep,
        string location,
        SendingProcessingMode? pmode)
    {
        if (!as4Message.HasSignalMessage)
        {
            _logger.LogTrace("No SignalMessages present to be inserted");
            return;
        }

        var relatedUserMessageIds = as4Message.SignalMessages
            .Where(m => !string.IsNullOrWhiteSpace(m.RefToMessageId))
            .Select(m => m.RefToMessageId!)
;
        var duplicateSignalMessages =
            DetermineDuplicateSignalMessageIds(relatedUserMessageIds);

        foreach (var signalMessage in as4Message.SignalMessages.Where(s => s is not PullRequest))
        {
            signalMessage.IsDuplicate = IsSignalMessageDuplicate(signalMessage, duplicateSignalMessages);

            try
            {
                var inMessage = InMessageBuilder
                    .ForSignalMessage(signalMessage, as4Message, mep)
                    .WithPMode(pmode)
                    .OnLocation(location)
                    .BuildAsToBeProcessed();

                _logger.LogDebug(
                    "Insert InMessage {SignalMessage} {MessageId} with {{Operation={Operation}, Status={Status}, PModeId={PModeId}}}",
                    signalMessage.GetType().Name,
                    signalMessage.MessageId,
                    inMessage.Operation,
                    inMessage.Status,
                    pmode?.Id);

                _repository.InsertInMessage(inMessage);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unable to insert SignalMessage {MessageId}", signalMessage.MessageId);

                throw new DataException($"Unable to insert SignalMessage {signalMessage.MessageId}", exception);
            }
        }
    }

    private IDictionary<string, bool> DetermineDuplicateSignalMessageIds(IEnumerable<string> searchedMessageIds)
    {
        var duplicateMessageIds = _repository.SelectExistingInRefToMessageIds(searchedMessageIds);

        return MergeTwoListsIntoADuplicateMessageMapping(searchedMessageIds, duplicateMessageIds);
    }

    private static IDictionary<string, bool> MergeTwoListsIntoADuplicateMessageMapping(
        IEnumerable<string> searchedMessageIds,
        IEnumerable<string> duplicateMessageIds)
    {
        return searchedMessageIds
            .Select(i => new KeyValuePair<string, bool>(i, duplicateMessageIds.Contains(i)))
            .ToDictionary(k => k.Key, v => v.Value);
    }

    /// <summary>
    /// Updates an <see cref="AS4Message"/> for delivery and notification.
    /// </summary>
    /// <param name="as4Message">The message.</param>
    /// <param name="receivingPMode"></param>
    /// <param name="sendingPMode"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <returns></returns>
    public void UpdateAS4MessageForMessageHandling(
        AS4Message as4Message,
        SendingProcessingMode? sendingPMode,
        ReceivingProcessingMode? receivingPMode)
    {
        if (as4Message.HasUserMessage)
        {
            var savedLocation = _bodyStore.SaveAS4Message(_configuration.InMessageStoreLocation, as4Message);

            var userMessageIds = as4Message.UserMessages.Select(u => u.MessageId);

            _repository.UpdateInMessages(
                m => userMessageIds.Any(id => id == m.EbmsMessageId),
                m => m.MessageLocation = savedLocation);
        }

        if (receivingPMode is not null
            && receivingPMode.MessageHandling?.MessageHandlingType == MessageHandlingChoiceType.Forward)
        {
            _logger.LogDebug("Received AS4Message must be forwarded since the ReceivingPMode {ReceivingPModeId} MessageHandling has a <Forward/> element", receivingPMode?.Id);

            var pmodeString = AS4XmlSerializer.ToString(receivingPMode);
            var pmodeId = receivingPMode?.Id;

            // Only set the Operation of the InMessage that represents the 
            // Primary Message-Unit to 'ToBeForwarded' since we want to prevent
            // that the same message is forwarded more than once (x number of messaging units 
            // present in the AS4 Message).

            _repository.UpdateInMessages(
                m => as4Message.MessageIds.Contains(m.EbmsMessageId),
                m =>
                {
                    m.Intermediary = true;
                    m.SetPModeInformation(pmodeId, pmodeString);
                    _logger.LogDebug("Update InMessage {EbmsMessageType} with {{Intermediary={Intermediary}, PMode={PModeId}}}", m.EbmsMessageType, m.Intermediary, pmodeId);
                });

            _repository.UpdateInMessage(
                as4Message.GetPrimaryMessageId(),
                m =>
                {
                    m.Operation = Operation.ToBeForwarded;
                    _logger.LogDebug("Update InMessage {EbmsMessageType} with Operation={Operation}", m.EbmsMessageType, m.Operation);
                });
        }
        else if (receivingPMode is not null
            && receivingPMode.MessageHandling?.MessageHandlingType == MessageHandlingChoiceType.Deliver)
        {
            UpdateUserMessagesForDelivery(as4Message.UserMessages, receivingPMode);
            UpdateSignalMessagesForNotification(as4Message.SignalMessages, sendingPMode);
        }
        else
        {
            UpdateSignalMessagesForNotification(as4Message.SignalMessages, sendingPMode);
        }
    }

    private void UpdateUserMessagesForDelivery(IEnumerable<UserMessage> userMessages, ReceivingProcessingMode receivingPMode)
    {
        if (!userMessages.Any())
        {
            _logger.LogTrace("No UserMessages present to be delivered");
            return;
        }

        var receivingPModeId = receivingPMode?.Id;
        var receivingPModeString = AS4XmlSerializer.ToString(receivingPMode);

        var xs = _repository
            .GetInMessagesData(userMessages.Select(um => um.MessageId), im => im.Id)
            .Zip(userMessages, Tuple.Create);

        foreach ((var id, var userMessage) in xs)
        {
            _repository.UpdateInMessage(
                userMessage.MessageId,
                message =>
                {
                    message.SetPModeInformation(receivingPModeId, receivingPModeString);

                    if (UserMessageNeedsToBeDelivered(receivingPMode, userMessage)
                        && !message.Intermediary)
                    {
                        message.Operation = Operation.ToBeDelivered;

                        var reliability =
                            receivingPMode?.MessageHandling?.DeliverInformation?.Reliability;

                        if (reliability?.IsEnabled ?? false)
                        {
                            var r = Entities.RetryReliability.CreateForInMessage(
                                refToInMessageId: id,
                                maxRetryCount: reliability.RetryCount,
                                retryInterval: reliability.RetryInterval.AsTimeSpan(),
                                type: RetryType.Delivery);

                            _logger.LogDebug(
                                "Insert RetryReliability for UserMessage InMessage {RefToInMessageId} with"
                                + " {{MaxRetryCount={MaxRetryCount}, RetryInterval={RetryInterval}}}",
                                r.RefToInMessageId,
                                r.MaxRetryCount,
                                r.RetryInterval);

                            _repository.InsertRetryReliability(r);
                        }
                        else
                        {
                            _logger.LogTrace(
                                "Will not insert RetryReliability for UserMessage(s) so it can be retried during delivery "
                                + "since the ReceivingPMode {PModeId} MessageHandling.Deliver.Reliability.IsEnabled = false",
                                receivingPMode?.Id);
                        }

                        _logger.LogDebug("Update InMessage UserMessage {MessageId} with Operation={Operation}",
                            userMessage.MessageId,
                            message.Operation);
                    }
                });
        }
    }

    private void UpdateSignalMessagesForNotification(IEnumerable<SignalMessage> signalMessages, SendingProcessingMode? sendingPMode)
    {
        if (!signalMessages.Any())
        {
            _logger.LogTrace("No SignalMessages present to be notified");
            return;
        }

        // Improvement: I think it will be safer if we retrieve the sending-pmodes of the related usermessages ourselves here
        // instead of relying on the SendingPMode that is available in the AS4Message object (which is set by another Step in the queue).
        var receipts = signalMessages.OfType<Receipt>();
        var notifyReceipts = sendingPMode?.ReceiptHandling?.NotifyMessageProducer ?? false;
        if (!notifyReceipts)
        {
            _logger.LogDebug("No Receipts will be notified since the SendingPMode {PModeId} ReceiptHandling.NotifyMessageProducer = false", sendingPMode?.Id);
        }

        var retryReceipts = sendingPMode?.ReceiptHandling?.Reliability;
        if (retryReceipts?.IsEnabled == false)
        {
            _logger.LogTrace(
                "Will not insert RetryReliability for Receipt(s) so it can be retried during delivery "
                + "since the ReceivingPMode {PModeId} ReceiptHandling.Reliability.IsEnabled = false",
                sendingPMode?.Id);
        }

        if (notifyReceipts)
        {
            UpdateSignalMessages(sendingPMode, receipts, retryReceipts);
        }

        UpdateReferencedUserMessagesStatus(receipts, OutStatus.Ack);

        var errors = signalMessages.OfType<Error>();
        var notifyErrors = sendingPMode?.ErrorHandling?.NotifyMessageProducer ?? false;
        if (!notifyErrors)
        {
            _logger.LogDebug("No Errors will be notified since the SendingPMode {PModeId} Errorhandling.NotifyMessageProducer = false", sendingPMode?.Id);
        }

        var retryErrors = sendingPMode?.ErrorHandling?.Reliability;
        if (retryErrors?.IsEnabled == false)
        {
            _logger.LogTrace(
                "Will not insert RetryReliability for Error(s) so it can be retried during notification "
                + "since the SendingPMode {PModeId} ErrorHandling.Reliability.IsEnabled = false",
                sendingPMode?.Id);
        }

        if (notifyErrors)
        {
            UpdateSignalMessages(sendingPMode, errors, retryErrors);
        }

        UpdateReferencedUserMessagesStatus(errors, OutStatus.Nack);
    }

    private void UpdateSignalMessages<TSignal>(
        SendingProcessingMode? sendingPMode,
        IEnumerable<TSignal> signalMessages,
        RetryReliability? reliability) where TSignal : SignalMessage
    {
        var signalsToNotify = signalMessages
            .Where(r => !r.IsDuplicate)
            .Select(s => s.MessageId)
            .ToArray();

        if (!signalsToNotify.Any())
        {
            return;
        }

        var ebmsMessageType = typeof(TSignal).Name;

        _repository.UpdateInMessages(
            m => signalsToNotify.Contains(m.EbmsMessageId) && !m.Intermediary,
            m =>
            {
                m.Operation = Operation.ToBeNotified;
                m.SetPModeInformation(sendingPMode);
                _logger.LogDebug("Update InMessage {EbmsMessageType} {EbmsMessageId} with Operation={Operation} according to SendingPMode {PModeId}",
                    ebmsMessageType,
                    m.EbmsMessageId,
                    m.Operation,
                    sendingPMode?.Id);
            });

        if (reliability?.IsEnabled == true)
        {
            var ids = _repository.GetInMessagesData(signalsToNotify, m => m.Id);
            foreach (var id in ids)
            {
                var r = Entities.RetryReliability.CreateForInMessage(
                    refToInMessageId: id,
                    maxRetryCount: reliability.RetryCount,
                    retryInterval: reliability.RetryInterval.AsTimeSpan(),
                    type: RetryType.Notification);

                _logger.LogDebug("Insert RetryReliability for SignalMessage InMessage {Id} with {{MaxRetryCount={MaxRetryCount}, RetryInterval={RetryInterval}}}",
                    id,
                    r.MaxRetryCount,
                    r.RetryInterval);

                _repository.InsertRetryReliability(r);
            }
        }
    }

    private void UpdateReferencedUserMessagesStatus(IEnumerable<SignalMessage> signalMessages, OutStatus outStatus)
    {
        var refToMessageIds = signalMessages.Select(r => r.RefToMessageId).Where(id => !string.IsNullOrEmpty(id)).ToArray();
        if (refToMessageIds.Any())
        {
            _repository.UpdateOutMessages(
                m => refToMessageIds.Contains(m.EbmsMessageId) && !m.Intermediary,
                m =>
                {
                    m.SetStatus(outStatus);
                    _logger.LogDebug("Update OutMessage UserMessage {EbmsMessageId} with Status={OutStatus}", m.EbmsMessageId, outStatus);
                });
        }
    }

    #region UserMessage related

    private bool IsUserMessageDuplicate(
        MessageUnit userMessage,
        IDictionary<string, bool> duplicateUserMessages)
    {
        duplicateUserMessages.TryGetValue(userMessage.MessageId, out var isDuplicate);

        if (isDuplicate)
        {
            _logger.LogDebug("[{MessageId}] Incoming User Message is a duplicated one", userMessage.MessageId);
        }

        return isDuplicate;
    }

    #endregion

    #region SignalMessage related

    private bool IsSignalMessageDuplicate(
        MessageUnit signalMessage,
        IDictionary<string, bool> duplicateSignalMessages)
    {
        if (string.IsNullOrWhiteSpace(signalMessage.RefToMessageId))
        {
            return false;
        }

        duplicateSignalMessages.TryGetValue(signalMessage.RefToMessageId, out var isDuplicate);

        if (isDuplicate)
        {
            _logger.LogDebug("[{RefToMessageId}] Incoming Signal Message is a duplicated one", signalMessage.RefToMessageId);
        }

        return isDuplicate;
    }

    #endregion SignalMessage related

    private bool UserMessageNeedsToBeDelivered(ReceivingProcessingMode? pmode, UserMessage userMessage)
    {
        if (pmode?.MessageHandling?.DeliverInformation == null)
        {
            _logger.LogDebug("UserMessage will not be delivered since the ReceivingPMode {PModeId} has not a MessageHandling.Deliver element",
                pmode?.Id);

            return false;
        }

        var needsToBeDelivered =
            pmode.MessageHandling.DeliverInformation.IsEnabled
            && !userMessage.IsDuplicate
            && !userMessage.IsTest;

        var message = $"UserMessage {(needsToBeDelivered ? "will" : "will not")} be delivered because the " +
            $"ReceivingPMode {pmode.Id} MessageHandling.Deliver.IsEnabled={pmode.MessageHandling.DeliverInformation.IsEnabled} and " +
            $"the UserMessage {(userMessage.IsTest ? "is" : "isn't")} a test message and {(userMessage.IsDuplicate ? "is" : "isn't")} a duplicate one";
        _logger.LogDebug(message);

        return needsToBeDelivered;
    }
}
