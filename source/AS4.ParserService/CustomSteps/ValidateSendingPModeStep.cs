using System.Configuration;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Validators;
using FluentValidation;

namespace AS4.ParserService.CustomSteps;

public class ValidateSendingPModeStep : IStep
{
    private readonly IValidator<SendingProcessingMode> _sendingProcessingModeValidator;

    public ValidateSendingPModeStep(IValidator<SendingProcessingMode> sendingProcessingModeValidator)
    {
        _sendingProcessingModeValidator = sendingProcessingModeValidator;
    }

    /// <summary>
    /// Validates whether the configured Sending PMode is valid and can be used.
    /// </summary>
    /// <param name="messagingContext">Message used during the step execution.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(messagingContext.SendingPMode);

        var result = _sendingProcessingModeValidator.Validate(messagingContext.SendingPMode);
        if (result.IsValid)
        {
            return StepResult.SuccessAsync(messagingContext);
        }

        var description = result.AppendValidationErrorsToErrorMessage($"Sending PMode {messagingContext.SendingPMode.Id} was invalid:");

        return StepResult.FailedAsync(new MessagingContext(new ConfigurationErrorsException(description)));
    }
}
