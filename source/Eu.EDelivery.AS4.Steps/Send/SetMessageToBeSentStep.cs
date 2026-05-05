using System.ComponentModel;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Services;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Send;

[Info("Confirm that the message can be sent.")]
[Description("Confirms that the message is ready to be sent.")]
public class SetMessageToBeSentStep : IStep
{
    private readonly ILogger<SetMessageToBeSentStep> _logger;
    private readonly IOutMessageService _outMessageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetMessageToBeSentStep"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="outMessageService"></param>
    public SetMessageToBeSentStep(ILogger<SetMessageToBeSentStep> logger, IOutMessageService outMessageService)
    {
        _logger = logger;
        _outMessageService = outMessageService;
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
                $"{nameof(SetMessageToBeSentStep)} requires an AS4Message to mark for sending but no AS4Message is present in the MessagingContext");
        }

        _logger.LogTrace("{LogTag} Set the message's Operation=ToBeSent", messagingContext.LogTag);
        if (messagingContext.MessageEntityId == null)
        {
            throw new InvalidOperationException(
                $"{messagingContext.LogTag} MessagingContext does not contain the ID of the OutMessage that must be set to ToBeSent");
        }

        _outMessageService.UpdateAS4MessageToBeSent(
            messagingContext.MessageEntityId.Value,
            messagingContext.AS4Message,
            messagingContext.SendingPMode?.Reliability?.ReceptionAwareness);


        return await StepResult.SuccessAsync(messagingContext);
    }
}

