using System.ComponentModel;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.Notify;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Services;
using Eu.EDelivery.AS4.Strategies.Sender;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Notify;

/// <summary>
/// Describes how a <see cref="NotifyMessage"/> is sent to the business application 
/// </summary>
[Info("Send notification message")]
[Description("Send a notification message using the method that is configured in the PMode")]
public class SendNotifyMessageStep : IStep
{
    private readonly ILogger<SendNotifyMessageStep> _logger;
    private readonly INotifySenderProvider _provider;
    private readonly IDatastoreRepository _repository;
    private readonly IMarkForRetryService _markForRetryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendNotifyMessageStep" /> class.
    /// Create a <see cref="IStep" /> implementation
    /// to send a <see cref="NotifyMessage" />
    /// to the consuming business application
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="provider">The provider.</param>
    /// <param name="repository"></param>
    /// <param name="markForRetryService"></param>
    public SendNotifyMessageStep(
        ILogger<SendNotifyMessageStep> logger,
        INotifySenderProvider provider,
        IDatastoreRepository repository,
        IMarkForRetryService markForRetryService)
    {
        _logger = logger;
        _provider = provider;
        _repository = repository;
        _markForRetryService = markForRetryService;
    }

    /// <summary>
    /// Start sending <see cref="NotifyMessage"/>
    /// to the consuming business application
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(messagingContext);

        if (messagingContext.NotifyMessage == null)
        {
            throw new InvalidOperationException(
                $"{nameof(SendNotifyMessageStep)} requires a NotifyMessage to send but no NotifyMessage is present in the MessagingContext");
        }

        if (messagingContext.NotifyMessage.StatusCode == Status.Delivered
            && messagingContext.SendingPMode == null)
        {
            throw new InvalidOperationException(
                $"{nameof(SendNotifyMessageStep)} requires a SendingPMode when the NotifyMessage is a Receipt that must be notified, "
                + "this is indicated by the NotifyMessage.StatusCode = Delivered");
        }

        if (messagingContext.NotifyMessage.StatusCode == Status.Error
            && messagingContext.SendingPMode == null)
        {
            throw new InvalidOperationException(
                $"{nameof(SendNotifyMessageStep)} requires a SendingPMode when the NotifyMessage is an Error that must be notified, "
                + "this is indicated by the NotifyMessage.StatusCode = Error");
        }

        if (messagingContext.NotifyMessage.StatusCode == Status.Exception
            && messagingContext.SendingPMode == null
            && messagingContext.ReceivingPMode == null)
        {
            throw new InvalidOperationException(
                $"{nameof(SendNotifyMessageStep)} requires either a SendingPMode ore ReceivingPMode when the NotifyMessage is an Exception that must be notified, "
                + "this is indicated by teh NotifyMessage.StatusCode = Exception");
        }

        var notifyMethod = GetNotifyMethodBasedOnNotifyMessage(
            messagingContext.NotifyMessage,
            messagingContext.SendingPMode,
            messagingContext.ReceivingPMode);

        if (messagingContext.SendingPMode == null)
        {
            var pmode = RetrieveSendingPModeForMessageWithEbmsMessageId(messagingContext.NotifyMessage.MessageInfo.RefToMessageId);
            if (pmode != null)
            {
                _logger.LogDebug(
                    "Using SendingPMode {PModeId} based on the NotifyMessage.MessageInfo.RefToMessageId "
                    + "{RefToMessageId} from the matching stored OutMessage",
                    pmode.Id,
                    messagingContext.NotifyMessage.MessageInfo.RefToMessageId);

                messagingContext.SendingPMode = pmode;
            }
        }

        _logger.LogTrace("Start sending NotifyMessage...");
        var result = await SendNotifyMessageAsync(notifyMethod, messagingContext.NotifyMessage, cancellation);
        _logger.LogTrace("NotifyMessage sent result in: {Result}", result);

        UpdateDatastore(
            messagingContext.NotifyMessage,
            messagingContext.MessageEntityId,
            result);

        return await StepResult.SuccessAsync(messagingContext);
    }

    private static Method GetNotifyMethodBasedOnNotifyMessage(
        NotifyMessageEnvelope notifyMessage,
        SendingProcessingMode? sendingPMode,
        ReceivingProcessingMode? receivingPMode)
    {
        switch (notifyMessage.StatusCode)
        {
            case Status.Delivered:
                if (sendingPMode?.ReceiptHandling?.NotifyMethod?.Type == null)
                {
                    throw new InvalidOperationException(
                        $"SendingPMode {sendingPMode?.Id} should have a ReceiptHandling.NotifyMethod "
                        + "with a <Type/> element indicating the notifying strategy when the NotifyMessage.StatusCode = Delivered. "
                        + "Default strategies are: 'FILE' and 'HTTP'. See 'Notify Uploading' for more information");
                }

                return sendingPMode.ReceiptHandling.NotifyMethod;
            case Status.Error:
                if (sendingPMode?.ErrorHandling?.NotifyMethod?.Type == null)
                {
                    throw new InvalidOperationException(
                        $"SendingPMode {sendingPMode?.Id} should have a ErrorHandling.NotifyMethod "
                        + "with a <Type/> element indicating the notifying strategy when the NotifyMessage.StatusCode = Error. "
                        + "Default strategies are: 'FILE' and 'HTTP'. See 'Notify Uploading' for more information");
                }

                return sendingPMode.ErrorHandling.NotifyMethod;
            case Status.Exception:
                if (sendingPMode?.Id != null)
                {
                    if (sendingPMode.ExceptionHandling?.NotifyMethod?.Type == null)
                    {
                        throw new InvalidOperationException(
                            $"SendingPMode {sendingPMode.Id} should have a ExceptionHandling.NotifyMethod "
                            + "with a <Type/> element indicating the notifying strategy when the NotifyMessage.StatusCode = Exception. "
                            + "This means that the NotifyMessage is an Exception occured during a outbound sending operation. "
                            + "Default strategies are: 'FILE' and 'HTTP'. See 'Notify Uploading' for more information");
                    }

                    return sendingPMode.ExceptionHandling.NotifyMethod;
                }

                if (receivingPMode?.ExceptionHandling?.NotifyMethod?.Type == null)
                {
                    throw new InvalidOperationException(
                        $"ReceivingPMode {receivingPMode?.Id} should have a ExceptionHandling.NotifyMethod "
                        + "with a <Type/> element indicating the notifying strategy when the NotifyMessage.StatusCode = Exception. "
                        + "This means that the NotifyMessage is an Exception occured during an inbound receiving operation. "
                        + "Default strategies are: 'FILE' and 'HTTP'. See 'Notify Uploading' for more information");
                }

                return receivingPMode.ExceptionHandling.NotifyMethod;
            default:
                throw new ArgumentOutOfRangeException($"No NotifyMethod not defined for status {notifyMessage.StatusCode}");
        }
    }

    private SendingProcessingMode? RetrieveSendingPModeForMessageWithEbmsMessageId(string? ebmsMessageId)
    {
        if (ebmsMessageId == null)
        {
            _logger.LogDebug("Can't retrieve SendingPMode because NotifyMessage.MessageInfo.RefToMessageId is not present");
            return null;
        }

        var outMessageData = _repository.GetOutMessageData(
                where: m => m.EbmsMessageId == ebmsMessageId && !m.Intermediary,
                selection: m => new { m.PMode, m.ModificationTime })
            .OrderByDescending(m => m.ModificationTime)
            .FirstOrDefault();

        if (outMessageData == null)
        {
            _logger.LogDebug(
                "Can't retrieve SendingPMode because no matching stored OutMessage found "
                + "for EbmsMessageId = {EbmsMessageId} AND Intermediary = false",
                ebmsMessageId);

            return null;
        }

        var pmode = AS4XmlSerializer.FromString<SendingProcessingMode>(outMessageData.PMode);
        if (pmode == null)
        {
            _logger.LogDebug(
                "Can't use SendingPMode from matching OutMessage for NotifyMessage.MessageInfo.RefToMessageId "
                + "{EbmsMessageId} because the PMode field can't be deserialized correctly to a SendingPMode",
                ebmsMessageId);
        }

        return pmode;
    }

    private async Task<SendResult> SendNotifyMessageAsync(Method notifyMethod, NotifyMessageEnvelope notifyMessage, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(notifyMethod.Type);

        var sender = _provider.GetNotifySender(notifyMethod);
        return await sender.SendAsync(notifyMessage, cancellation);
    }

    private void UpdateDatastore(
        NotifyMessageEnvelope notifyMessage,
        long? messageEntityId,
        SendResult result)
    {

        if (!messageEntityId.HasValue)
        {
            throw new InvalidOperationException(
                $"Unable to update notified entities of type {notifyMessage.EntityType?.FullName} because no entity id is present");
        }

        if (notifyMessage.EntityType == typeof(InMessage))
        {
            _markForRetryService.UpdateNotifyMessageForIncomingMessage(messageEntityId.Value, result);
        }
        else if (notifyMessage.EntityType == typeof(OutMessage))
        {
            _markForRetryService.UpdateNotifyMessageForOutgoingMessage(messageEntityId.Value, result);
        }
        else if (notifyMessage.EntityType == typeof(InException))
        {
            _markForRetryService.UpdateNotifyExceptionForIncomingMessage(messageEntityId.Value, result);
        }
        else if (notifyMessage.EntityType == typeof(OutException))
        {
            _markForRetryService.UpdateNotifyExceptionForOutgoingMessage(messageEntityId.Value, result);
        }
        else
        {
            throw new InvalidOperationException(
                $"Unable to update notified entities of type {notifyMessage.EntityType?.FullName}."
                + "Please provide one of the following types in the notify message: "
                + "InMessage, OutMessage, InException, and OutException are supported");
        }
    }
}
