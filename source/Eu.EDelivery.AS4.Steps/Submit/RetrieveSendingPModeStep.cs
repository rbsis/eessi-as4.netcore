using System.ComponentModel;
using System.Configuration;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Validators;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Submit;

/// <summary>
/// Add the retrieved PMode to the <see cref="Model.Submit.SubmitMessage" />
/// after the PMode is verified
/// </summary>
[Info("Retrieve SendingPMode")]
[Description("Retrieve the SendingPMode that must be used to send the AS4Message")]
public class RetrieveSendingPModeStep : IStep
{
    private readonly ILogger<RetrieveSendingPModeStep> _logger;
    private readonly IConfig _config;
    private readonly IValidator<SendingProcessingMode> _sendingProcessingModeValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetrieveSendingPModeStep" /> class
    /// Create a new Retrieve PMode Step with a given <see cref="IConfig" />
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="config">
    /// </param>
    /// <param name="sendingProcessingModeValidator"></param>
    public RetrieveSendingPModeStep(
        ILogger<RetrieveSendingPModeStep> logger,
        IConfig config,
        IValidator<SendingProcessingMode> sendingProcessingModeValidator)
    {
        _config = config;
        _logger = logger;
        _sendingProcessingModeValidator = sendingProcessingModeValidator;
    }

    /// <summary>
    /// Retrieve the PMode that must be used to send the SubmitMessage that is in the current Messagingcontext />
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(messagingContext);

        if (messagingContext.SubmitMessage == null)
        {
            throw new InvalidOperationException(
                $"{nameof(RetrieveSendingPModeStep)} requires an SubmitMessage to retrieve the SendingPMode from but no SubmitMessage is present in the MessagingContext");
        }

        messagingContext.SubmitMessage.PMode = RetrieveSendPMode(messagingContext);
        messagingContext.SendingPMode = messagingContext.SubmitMessage.PMode;

        return await StepResult.SuccessAsync(messagingContext);
    }

    private SendingProcessingMode RetrieveSendPMode(MessagingContext message)
    {
        var pmode = RetrievePMode(message);
        ValidatePMode(pmode);

        return pmode;
    }

    private SendingProcessingMode RetrievePMode(MessagingContext context)
    {
        var processingModeId = RetrieveProcessingModeId(context.SubmitMessage?.Collaboration);

        var pmode = _config.GetSendingPMode(processingModeId)
            ?? throw new InvalidOperationException($"SendingPMode {processingModeId} was not retrieved for SubmitMessage");

        _logger.LogInformation("{LogTag} SendingPMode \"{PModeId}\" was successfully retrieved for SubmitMessage",
            context.LogTag,
            pmode.Id);

        return pmode;
    }

    private string RetrieveProcessingModeId(Model.Common.CollaborationInfo? collaborationInfo)
    {
        if (collaborationInfo?.AgreementRef?.PModeId == null)
        {
            _logger.LogError(
                "SubmitMessage is incomplete to retrieve the SendingPMode because the Collaboration.AgreementRef.PModeId element is missing");

            throw new InvalidOperationException(
                "SubmitMessage is incomplete to retrieve the SendingPMode because the Collaboration.AgreementRef.PModeId element is missing");
        }

        return collaborationInfo.AgreementRef.PModeId;
    }

    private void ValidatePMode(SendingProcessingMode pmode)
    {
        _sendingProcessingModeValidator.Validate(pmode).Result(
            onValidationSuccess: result => _logger.LogTrace("SendingPMode {PModeId} is valid for Submit Message", pmode.Id),
            onValidationFailed: result =>
            {
                var description = result.AppendValidationErrorsToErrorMessage(
                    $"SendingPMode {pmode.Id} was invalid and cannot be used to assign to the SubmitMessage: ");

                _logger.LogError(description);

                throw new ConfigurationErrorsException(description);
            });
    }
}
