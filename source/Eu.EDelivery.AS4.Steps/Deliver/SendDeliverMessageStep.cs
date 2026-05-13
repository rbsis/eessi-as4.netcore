using System.ComponentModel;
using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Services;
using Eu.EDelivery.AS4.Strategies.Sender;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Deliver;

/// <summary>
/// Describes how a DeliverMessage is sent to the consuming business application. 
/// </summary>
[Info("Send deliver message to the configured business application endpoint")]
[Description("Send deliver message to the configured business application endpoint")]
public class SendDeliverMessageStep : IStep
{
    private readonly ILogger<SendDeliverMessageStep> _logger;
    private readonly IDeliverSenderProvider _messageProvider;
    private readonly IMarkForRetryService _markForRetryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendDeliverMessageStep"/> class
    /// Create a <see cref="IStep"/> implementation
    /// for sending the Deliver Message to the consuming business application
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="messageProvider"> The message sender provider.</param>
    /// <param name="markForRetryService"></param>
    public SendDeliverMessageStep(
        ILogger<SendDeliverMessageStep> logger,
        IDeliverSenderProvider messageProvider,
        IMarkForRetryService markForRetryService)
    {
        _logger = logger;
        _messageProvider = messageProvider;
        _markForRetryService = markForRetryService;
    }

    /// <summary>
    /// Start sending the AS4 Messages 
    /// to the consuming business application
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        if (messagingContext.DeliverMessage == null)
        {
            throw new InvalidOperationException("Unable to send DeliverMessage: no DeliverMessage is set");
        }

        if (messagingContext.DeliverMessage.Message.MessageInfo.MessageId == null)
        {
            throw new InvalidOperationException("Unable to send DeliverMessage: no MessageId is set");
        }

        if (messagingContext.ReceivingPMode == null)
        {
            throw new InvalidOperationException("Unable to send DeliverMessage: no ReceivingPMode is set");
        }

        if (messagingContext.ReceivingPMode.MessageHandling.DeliverInformation == null)
        {
            throw new InvalidOperationException(
                $"Unable to send the DeliverMessage: the ReceivingPMode {messagingContext.ReceivingPMode.Id} does not contain any <DeliverInformation />." +
                "Please provide a correct <DeliverInformation /> tag to indicate where the deliver message (and its attachments) should be send to.");
        }

        if (messagingContext.ReceivingPMode.MessageHandling.DeliverInformation.DeliverMethod.Type == null)
        {
            throw new InvalidOperationException(
                $"Unable to send the DeliverMessage: the ReceivingPMode {messagingContext.ReceivingPMode.Id} "
                + "does not contain any <Type/> element indicating the uploading strategy in the MessageHandling.Deliver.DeliverMethod element. "
                + "Default sending strategies are: 'FILE' and 'HTTP'. See 'Deliver Uploading' for more information");
        }

        _logger.LogTrace("{LogTag} Start sending the DeliverMessage to the consuming business application...", messagingContext.LogTag);
        var result = await SendDeliverMessageAsync(
                messagingContext.ReceivingPMode.MessageHandling.DeliverInformation.DeliverMethod,
                messagingContext.DeliverMessage, cancellation);
        _logger.LogTrace("{LogTag} Done sending the DeliverMesssage to the consuming business application", messagingContext.LogTag);

        _markForRetryService.UpdateDeliverMessageForDeliverResult(messagingContext.DeliverMessage.Message.MessageInfo.MessageId, result);

        return await StepResult.SuccessAsync(messagingContext);
    }

    private async Task<SendResult> SendDeliverMessageAsync(
        Method deliverMethod,
        DeliverMessageEnvelope deliverMessage,
        CancellationToken cancellation)
    {
        ArgumentException.ThrowIfNullOrEmpty(deliverMethod.Type);

        var sender = _messageProvider.GetDeliverSender(deliverMethod);
        return await sender.SendAsync(deliverMessage, cancellation);
    }
}
