using System.Collections.ObjectModel;
using System.ComponentModel;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Services;
using Eu.EDelivery.AS4.Strategies.Sender;
using Eu.EDelivery.AS4.Strategies.Uploader;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Deliver;

/// <summary>
/// Describes how the message payloads are uploaded to their respective media
/// </summary>
[Info("Upload attachments to deliver location")]
[Description("This step uploads the deliver message payloads to the destination that was configured in the receiving pmode.")]
public class UploadAttachmentsStep : IStep
{
    private readonly ILogger<UploadAttachmentsStep> _logger;
    private readonly IAttachmentUploaderProvider _provider;
    private readonly IMarkForRetryService _markForRetryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadAttachmentsStep" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="provider">The provider.</param>
    /// <param name="markForRetryService"></param>
    public UploadAttachmentsStep(
        ILogger<UploadAttachmentsStep> logger,
        IAttachmentUploaderProvider provider,
        IMarkForRetryService markForRetryService)
    {
        _logger = logger;
        _provider = provider;
        _markForRetryService = markForRetryService;
    }

    /// <summary>
    /// Start uploading the AS4 Message Payloads
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        if (messagingContext.DeliverMessage == null)
        {
            throw new InvalidOperationException(
                $"{nameof(UploadAttachmentsStep)} requires a DeliverMessage to upload the attachments from but no DeliverMessage is present in the MessagingContext");
        }

        if (messagingContext.DeliverMessage.Message.MessageInfo.MessageId == null)
        {
            throw new InvalidOperationException("Unable to send DeliverMessage: no MessageId is set");
        }

        var deliverEnvelope = messagingContext.DeliverMessage;
        if (!deliverEnvelope.Attachments.Any())
        {
            _logger.LogDebug("(Deliver) No attachments to upload for DeliverMessage");
            return await StepResult.SuccessAsync(messagingContext);
        }

        if (messagingContext.ReceivingPMode == null)
        {
            throw new InvalidOperationException("Unable to send DeliverMessage: no ReceivingPMode is set");
        }

        if (messagingContext.ReceivingPMode.MessageHandling.DeliverInformation?.PayloadReferenceMethod == null)
        {
            throw new InvalidOperationException(
                $"Unable to send the DeliverMessage: the ReceivingPMode {messagingContext.ReceivingPMode.Id} "
                + "does not contain any <PayloadReferenceMethod/> element in the MessageHandling.Deliver element. "
                + "Please provide a correct <PayloadReferenceMethod/> tag to indicate where the attachments of the DeliverMessage should be sent to.");
        }

        if (messagingContext.ReceivingPMode.MessageHandling.DeliverInformation.PayloadReferenceMethod.Type == null)
        {
            throw new InvalidOperationException(
                $"Unable to send the DeliverMessage: the ReceivingPMode {messagingContext.ReceivingPMode.Id} "
                + "does not contain any <Type/> element in the MessageHandling.Deliver.PayloadReferenceMethod element "
                + "that indicates which uploading strategy that must be used."
                + "Default uploading strategies are: 'FILE' and 'HTTP'. See 'Deliver Uploading' for more information");
        }

        var uploader = GetAttachmentUploader(messagingContext.ReceivingPMode);
        var results = new Collection<UploadResult>();

        foreach (var att in deliverEnvelope.Attachments)
        {
            var result = await TryUploadAttachmentAsync(att, deliverEnvelope, uploader, cancellation);
            results.Add(result);
        }

        var accResult = results
            .Select(r => r.Status)
            .Aggregate(SendResultUtils.Reduce);

        _markForRetryService.UpdateDeliverMessageForUploadResult(deliverEnvelope.Message.MessageInfo.MessageId, accResult);

        if (accResult == SendResult.Success)
        {
            return await StepResult.SuccessAsync(messagingContext);
        }

        return await StepResult.FailedAsync(messagingContext);
    }

    private IAttachmentUploader GetAttachmentUploader(ReceivingProcessingMode pmode)
    {
        var payloadReferenceMethod = pmode.MessageHandling!.DeliverInformation!.PayloadReferenceMethod;
        var uploader = _provider.Get(payloadReferenceMethod.Type!);
        uploader.Configure(payloadReferenceMethod);
        return uploader;
    }

    private async Task<UploadResult> TryUploadAttachmentAsync(
        Attachment attachment,
        DeliverMessageEnvelope deliverMessage,
        IAttachmentUploader uploader,
        CancellationToken cancellation)
    {
        try
        {
            _logger.LogTrace("Start Uploading Attachment {AttachmentId}...", attachment.Id);
            var attachmentResult = await uploader.UploadAsync(attachment, deliverMessage.Message.MessageInfo, cancellation)
                ?? throw new ArgumentNullException(nameof(uploader), $@"{uploader.GetType().Name} returns 'null' for Attachment {attachment.Id}");

            attachment.ResetContentPosition();

            var referencedPayload = deliverMessage.Message.Payloads.FirstOrDefault(attachment.Matches)
                ?? throw new InvalidOperationException($"No referenced <Payload/> element found in DeliverMessage to assign the upload location to with attachment Id = {attachment.Id}");
            referencedPayload.Location = attachmentResult.DownloadUrl;

            _logger.LogTrace("Attachment {AttachmentId} uploaded succesfully", attachment.Id);
            return attachmentResult;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Attachment {AttachmentId} cannot be uploaded because of an exception", attachment.Id);
            return UploadResult.FatalFail;
        }
    }
}
