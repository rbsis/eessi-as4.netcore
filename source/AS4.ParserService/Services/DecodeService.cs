using AS4.ParserService.Infrastructure;
using AS4.ParserService.Models;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Serialization;

namespace AS4.ParserService.Services;

public class DecodeService
{
    private readonly ILogger<DecodeService> _logger;
    private readonly ISerializerProvider _serializerProvider;
    private readonly StepProcessor _stepProcessor;

    public DecodeService(ILogger<DecodeService> logger, ISerializerProvider serializerProvider, StepProcessor stepProcessor)
    {
        _logger = logger;
        _serializerProvider = serializerProvider;
        _stepProcessor = stepProcessor;
    }

    public async Task<DecodeResult> ProcessAsync(DecodeMessageInfo info, CancellationToken cancellation)
    {
        // Shortcuts have been taken.
        // Theoretically, it is possible that we receive an AS4Message that contains multiple 
        // message-parts.  We should process each message-part seperately.

        using var receivedStream = new MemoryStream(info.ReceivedMessage);
        var as4Message = await RetrieveAS4MessageAsync(info.ContentType, receivedStream, cancellation);

        if (as4Message == null)
        {
            return DecodeResult.CreateForBadRequest();
        }

        if (as4Message.IsSignalMessage)
        {
            return DecodeResult.CreateAccepted(
                as4Message.FirstSignalMessage is Receipt ? EbmsMessageType.Receipt : EbmsMessageType.Error,
                as4Message.GetPrimaryMessageId()!,
                (as4Message.FirstSignalMessage as Error));
        }

        // Start Processing
        var receivingPMode = await AssembleReceivingPModeAsync(info, cancellation);
        var respondingPMode = await AssembleRespondingPModeAsync(info, cancellation);

        if (receivingPMode == null)
        {
            return DecodeResult.CreateForBadRequest();
        }

        if (respondingPMode == null)
        {
            return DecodeResult.CreateForBadRequest();
        }

        var context = CreateMessagingContext(as4Message, receivingPMode, respondingPMode);

        try
        {
            var decodeResult = await PerformInboundProcessingAsync(context, cancellation);

            return decodeResult;
        }
        finally
        {
            context.Dispose();
        }
    }

    private static MessagingContext CreateMessagingContext(AS4Message receivedAS4Message, ReceivingProcessingMode receivingPMode, SendingProcessingMode respondingPMode)
    {
        var context = new MessagingContext(receivedAS4Message, MessagingContextMode.Receive)
        {
            ReceivingPMode = receivingPMode,
            SendingPMode = respondingPMode
        };

        return context;
    }

    private async Task<DecodeResult> PerformInboundProcessingAsync(MessagingContext context, CancellationToken cancellation)
    {
        var processingResult = await _stepProcessor.ExecuteStepsAsync(
            context,
            StepRegistry.InboundProcessingConfiguration,
            cancellation);
        if (processingResult.AS4Message == null)
        {
            throw new InvalidOperationException("An error occured while decoding the AS4 Message", processingResult.Exception);
        }

        if (processingResult.AS4Message.IsUserMessage)
        {
            try
            {
                var deliverPayloads = RetrievePayloadsFromMessage(processingResult.AS4Message);
                var receivedMessageId = processingResult.EbmsMessageId;
                var receiptResult = await _stepProcessor.ExecuteStepsAsync(
                    processingResult,
                    StepRegistry.ReceiptCreationConfiguration,
                    cancellation);
                if (receiptResult.AS4Message == null)
                {
                    throw new InvalidOperationException("An unexpected error occured while creating the AS4 Receipt message", receiptResult.Exception);
                }

                return DecodeResult.CreateWithReceipt([.. deliverPayloads],
                                                      _serializerProvider.ToByteArray(receiptResult.AS4Message),
                                                      receivedMessageId!,
                                                      receiptResult.AS4Message.GetPrimaryMessageId() ?? string.Empty);
            }
            finally
            {
                processingResult.Dispose();
            }
        }

        if (processingResult.AS4Message.FirstSignalMessage is not Error)
        {
            throw new InvalidProgramException("An AS4 Error Message was expected.");
        }

        // What we have now, must an error.
        return DecodeResult.CreateWithError(_serializerProvider.ToByteArray(processingResult.AS4Message),
                                            ((Error)processingResult.AS4Message.FirstSignalMessage),
                                            processingResult.AS4Message.FirstSignalMessage.RefToMessageId ?? string.Empty,
                                            processingResult.AS4Message.FirstSignalMessage.MessageId);
    }

    private static IEnumerable<PayloadInfo> RetrievePayloadsFromMessage(AS4Message message)
    {
        foreach (var attachment in message.Attachments)
        {
            yield return new PayloadInfo(attachment.Id, attachment.ContentType, attachment.Content.ToBytes());
        }
    }

    private static async Task<ReceivingProcessingMode?> AssembleReceivingPModeAsync(DecodeMessageInfo info, CancellationToken cancellation)
    {
        if (info.ReceivingPMode == null)
        {
            return null;
        }

        var pmode = await Deserializer.ToReceivingPModeAsync(info.ReceivingPMode, cancellation);
        if (pmode == null)
        {
            return null;
        }

        if (info.DecryptionCertificate != null && info.DecryptionCertificate.Length > 0)
        {
            pmode.Security.Decryption.DecryptCertificateInformation = new PrivateKeyCertificate()
            {
                Certificate = Convert.ToBase64String(info.DecryptionCertificate),
                Password = info.DecryptionCertificatePassword
            };
        }

        return pmode;
    }

    private static async Task<SendingProcessingMode?> AssembleRespondingPModeAsync(DecodeMessageInfo info, CancellationToken cancellation)
    {
        if (info.RespondingPMode == null)
        {
            return null;
        }

        var pmode = await Deserializer.ToSendingPModeAsync(info.RespondingPMode, cancellation);
        if (pmode == null)
        {
            return null;
        }

        if (pmode.Security?.Signing?.IsEnabled ?? false)
        {
            pmode.Security.Signing.SigningCertificateInformation = new PrivateKeyCertificate
            {
                Certificate = Convert.ToBase64String(info.SigningResponseCertificate ?? []),
                Password = info.SigningResponseCertificatePassword
            };
        }

        return pmode;
    }

    private async Task<AS4Message?> RetrieveAS4MessageAsync(string contentType, Stream receivedStream, CancellationToken cancellation)
    {
        try
        {
            var deserializer = _serializerProvider.Get(contentType);
            return await deserializer.DeserializeAsync(receivedStream, contentType, cancellation);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Deserialize AS4Message failed");
            return null;
        }
    }
}
