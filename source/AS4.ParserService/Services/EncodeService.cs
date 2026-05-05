using AS4.ParserService.Infrastructure;
using AS4.ParserService.Models;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Model.Submit;
using Eu.EDelivery.AS4.Steps.Submit;

namespace AS4.ParserService.Services;

public partial class EncodeService
{
    private readonly CreateAS4MessageStep _createAS4MessageStep;
    private readonly StepProcessor _stepProcessor;

    public EncodeService(CreateAS4MessageStep createAS4MessageStep, StepProcessor stepProcessor)
    {
        _createAS4MessageStep = createAS4MessageStep;
        _stepProcessor = stepProcessor;
    }

    internal async Task<MessagingContext?> CreateAS4MessageAsync(EncodeMessageInfo encodeInfo, CancellationToken cancellation)
    {
        var pmode = await AssembleSendingPModeAsync(encodeInfo, cancellation);
        if (pmode == null)
        {
            return null;
        }

        var as4Message = await AssembleAS4MessageAsync(pmode, encodeInfo.Payloads, cancellation);

        var context = SetupMessagingContext(as4Message, pmode);

        return await _stepProcessor.ExecuteStepsAsync(context, StepRegistry.OutboundProcessingStepConfiguration, cancellation);
    }

    private static async Task<SendingProcessingMode?> AssembleSendingPModeAsync(EncodeMessageInfo encodeInfo, CancellationToken cancellation)
    {
        if (encodeInfo.SendingPMode == null)
        {
            return null;
        }

        var pmode = await Deserializer.ToSendingPModeAsync(encodeInfo.SendingPMode, cancellation);
        if (pmode == null)
        {
            return null;
        }

        if (pmode.Security?.Signing?.IsEnabled ?? false)
        {
            pmode.Security.Signing.SigningCertificateInformation = new PrivateKeyCertificate
            {
                Certificate = Convert.ToBase64String(encodeInfo.SigningCertificate ?? []),
                Password = encodeInfo.SigningCertificatePassword
            };
        }

        if (pmode.Security?.Encryption?.IsEnabled ?? false)
        {
            pmode.Security.Encryption.EncryptionCertificateInformation = new PublicKeyCertificate
            {
                Certificate = Convert.ToBase64String(encodeInfo.EncryptionPublicKeyCertificate ?? [])
            };
        }

        return pmode;
    }

    private async Task<AS4Message> AssembleAS4MessageAsync(
        SendingProcessingMode pmode,
        IEnumerable<PayloadInfo> payloads,
        CancellationToken cancellation)
    {
        var submitMessage = new SubmitMessage
        {
            PMode = pmode,
            Payloads = [.. payloads.Select(p => new Payload(p.PayloadName, "", p.ContentType))]
        };

        //var createAS4MessageStep = new CreateAS4MessageStep(
        //    submitPayload => new InMemoryPayloadRetriever(
        //        payloads.First(p => p.PayloadName == submitPayload.Id)))

        var ctx = new MessagingContext(submitMessage) { SendingPMode = pmode };
        var stepResult = await _createAS4MessageStep.ExecuteAsync(ctx, cancellation);
        return stepResult.MessagingContext.AS4Message!;
    }

    private static MessagingContext SetupMessagingContext(AS4Message as4Message, SendingProcessingMode sendingPMode)
    {
        return new(as4Message, MessagingContextMode.Submit)
        {
            SendingPMode = sendingPMode
        };
    }
}
