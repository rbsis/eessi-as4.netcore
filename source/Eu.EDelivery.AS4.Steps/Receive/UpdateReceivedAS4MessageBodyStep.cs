using System.ComponentModel;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Services;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Receive;

[Info("Update the received AS4 Message")]
[Description("Updates the AS4 Message that has been received after processing so that it can be delivered or forwarded")]
public class UpdateReceivedAS4MessageBodyStep : IStep
{
    private readonly ILogger<UpdateReceivedAS4MessageBodyStep> _logger;
    private readonly IInMessageService _inMessageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateReceivedAS4MessageBodyStep" /> class.
    /// </summary>
    public UpdateReceivedAS4MessageBodyStep(
        ILogger<UpdateReceivedAS4MessageBodyStep> logger,
        IInMessageService inMessageService)
    {
        _logger = logger;
        _inMessageService = inMessageService;
    }

    /// <summary>
    /// Execute the step for a given <paramref name="messagingContext"/>.
    /// </summary>
    /// <param name="messagingContext">Message used during the step execution.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        if (messagingContext?.AS4Message == null)
        {
            throw new InvalidOperationException(
                $"{nameof(UpdateReceivedAS4MessageBodyStep)} requires an AS4Message to update but no AS4Message is present in the MessagingContext");
        }

        _logger.LogTrace("Updating the received message body...");
        _inMessageService.UpdateAS4MessageForMessageHandling(
            messagingContext.AS4Message,
            messagingContext.SendingPMode,
            messagingContext.ReceivingPMode);


        if (messagingContext.ReceivedMessageMustBeForwarded)
        {
            // When the Message has to be forwarded, the remaining Steps must not be executed.
            // The MSH must answer with a HTTP Accepted status-code, so an empty context must be returned.
            messagingContext.ModifyContext(AS4Message.Empty);

            _logger.LogInformation(
                "Stops execution to return empty SOAP envelope to the orignal sender. " +
                "This happens when the message must be forwarded");

            return (await StepResult.SuccessAsync(messagingContext)).AndStopExecution();
        }

        return await StepResult.SuccessAsync(messagingContext);
    }
}
