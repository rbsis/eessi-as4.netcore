using System.ComponentModel;
using System.Configuration;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Model.Internal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Forward;

[Info("Determine routing for message that must be forwarded")]
[Description("Determine how the message must be forwarded by retrieving the sending pmode that must be used.")]
public class DetermineRoutingStep : IStep
{
    private readonly ILogger<DetermineRoutingStep> _logger;
    private readonly IConfig _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DetermineRoutingStep"/> class.
    /// </summary>
    public DetermineRoutingStep(ILogger<DetermineRoutingStep> logger, IConfig configuration)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Execute the step for a given <paramref name="messagingContext"/>.
    /// </summary>
    /// <param name="messagingContext">Message used during the step execution.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        if (messagingContext.ReceivedMessage == null)
        {
            throw new NotSupportedException(
                "DetermineRoutingStep requires a 'ReceivedMessage'");
        }

        if (messagingContext.ReceivingPMode == null)
        {
            throw new InvalidOperationException(
                "No ReceivingPMode available is set." +
                "The ReceivingPMode is used to correctly forward the message with the provided configured values inside this PMode.");
        }

        if (messagingContext.ReceivingPMode.MessageHandling?.ForwardInformation == null)
        {
            throw new ConfigurationErrorsException(
                "The ReceivingPMode does not contain a MessageHandling.Forward element." +
                "This element is required in a Forwarding scenario.");
        }

        var sendingPModeId = messagingContext.ReceivingPMode.MessageHandling.ForwardInformation.SendingPMode;
        _logger.LogTrace("SendingPMode {SendingPModeId} must be used to forward Message with Id {EbmsMessageId}",
            sendingPModeId,
            messagingContext.EbmsMessageId);

        if (string.IsNullOrWhiteSpace(sendingPModeId))
        {
            throw new ConfigurationErrorsException(
                "The ReceivingPMode does not contain a SendingPMode-Id in the MessageHandling.Forward element." +
                "This SendingPMode-Id is required in a Forwarding scenario and will be used to forward the message to the next MSH.");
        }

        var sendingPMode = _configuration.GetSendingPMode(sendingPModeId) ?? throw new ConfigurationErrorsException(
                $"No Sending Processing Mode found for {sendingPModeId}." +
                "Please provide a valid Id that points to a configured Sending PMode." +
                @"SendingPModes are configured in the .\config\send-pmodes\ folder.");

        messagingContext.SendingPMode = sendingPMode;
        return StepResult.SuccessAsync(messagingContext);
    }
}
