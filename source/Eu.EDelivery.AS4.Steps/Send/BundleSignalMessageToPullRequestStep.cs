using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Services;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Send;

/// <summary>
/// Adds piggy-backed ebMS signal messages to the <see cref="PullRequest"/> for signal messages that are responses
/// of ebMS user messages that matches the <see cref="PullRequest.Mpc"/>.
/// </summary>
public class BundleSignalMessageToPullRequestStep : IStep
{
    private readonly ILogger<BundleSignalMessageToPullRequestStep> _logger;
    private readonly IPiggyBackingService _piggyBackingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BundleSignalMessageToPullRequestStep"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="piggyBackingService"></param>
    public BundleSignalMessageToPullRequestStep(
        ILogger<BundleSignalMessageToPullRequestStep> logger,
        IPiggyBackingService piggyBackingService)
    {
        _logger = logger;
        _piggyBackingService = piggyBackingService;
    }

    /// <summary>
    /// Execute the step on a given <paramref name="messagingContext"/>.
    /// </summary>
    /// <param name="messagingContext"><see cref="MessagingContext"/> on which the step must be executed.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(messagingContext);

        if (messagingContext.AS4Message == null)
        {
            throw new InvalidOperationException(
                $"{nameof(BundleSignalMessageToPullRequestStep)} requires a AS4Message to possible bundle a "
                + "SignalMessage to the PullRequest but there's not a AS4Message present in the MessagingContext");
        }

        if (messagingContext.SendingPMode == null)
        {
            throw new InvalidOperationException(
                $"{nameof(BundleSignalMessageToPullRequestStep)} requires a SendingPMode to select the right "
                + "SignalMessages for piggybacking but there's not a SendingPMode present in the MessagingContext");
        }

        if (messagingContext.AS4Message.PrimaryMessageUnit is not PullRequest pullRequest)
        {
            throw new InvalidOperationException(
                $"{nameof(BundleSignalMessageToPullRequestStep)} requires a PullRequest as primary message unit in the "
                + "AS4Message but there's not a PullRequest present in the MessagingContext");
        }

        var signals = await _piggyBackingService.SelectToBePiggyBackedSignalMessagesAsync(pullRequest, messagingContext.SendingPMode, cancellation);

        foreach (var signal in signals)
        {
            _logger.LogInformation("PiggyBack the {Name} \"{MessageId}\" which reference UserMessage \"{RefToMessageId}\" to the PullRequest",
                signal.GetType().Name,
                signal.MessageId,
                signal.RefToMessageId);

            messagingContext.AS4Message.AddMessageUnit(signal);
        }

        return await StepResult.SuccessAsync(messagingContext);
    }
}
