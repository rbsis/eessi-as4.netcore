using System.ComponentModel;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Services;
using Eu.EDelivery.AS4.Services.Journal;
using Microsoft.Extensions.Logging;
using MessageExchangePattern = Eu.EDelivery.AS4.Entities.MessageExchangePattern;

namespace Eu.EDelivery.AS4.Steps.Receive;

/// <summary>
/// Describes how the data store gets updated when an incoming message is received.
/// </summary>
[Info("Save received message")]
[Description("Saves a received message as-is in the datastore.")]
public class SaveReceivedMessageStep : IStep
{
    private readonly ILogger<SaveReceivedMessageStep> _logger;
    private readonly IInMessageService _inMessageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveReceivedMessageStep"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="inMessageService"></param>
    public SaveReceivedMessageStep(
        ILogger<SaveReceivedMessageStep> logger,
        IInMessageService inMessageService)
    {
        _logger = logger;
        _inMessageService = inMessageService;
    }

    /// <summary>
    /// Start updating the Data store
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <exception cref="Exception">A delegate callback throws an exception.</exception>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(messagingContext);

        if (messagingContext.ReceivedMessage == null)
        {
            throw new InvalidOperationException(
                $"{nameof(SaveReceivedMessageStep)} requires a ReceivedMessage to store the incoming message into the datastore but no ReceivedMessage is present in the MessagingContext");
        }

        if (messagingContext.AS4Message == null)
        {
            throw new InvalidOperationException(
                $"{nameof(SaveReceivedMessageStep)} requires an AS4Message to save but no AS4Message is present in the MessagingContext");
        }

        _logger.LogTrace("{LogTag} Store the incoming AS4 Message to the datastore", messagingContext.LogTag);
        var resultContext = await InsertReceivedAS4MessageAsync(messagingContext, cancellation);

        if (resultContext.Exception == null)
        {
            /* PullRequests will not be notified, so we don't need a referenced Id,
             * Receipts and Errors will need to have a referenced UserMessage 
             * if we want to know the original Sending PMode required for the notification. */

            var primaryMessageUnit = resultContext.AS4Message!.PrimaryMessageUnit;
            if ((primaryMessageUnit is Receipt || primaryMessageUnit is Error)
                && string.IsNullOrWhiteSpace(primaryMessageUnit.RefToMessageId))
            {
                _logger.LogWarning(
                    "{LogTag} Received message is a SignalMessage without RefToMessageId. " +
                    "No such SignalMessage are supported so the message cannot be processed any further",
                    messagingContext.LogTag);

                return (await StepResult.SuccessAsync(new MessagingContext(AS4Message.Empty, messagingContext.Mode)))
                    .AndStopExecution();
            }
            _logger.LogTrace("{LogTag} The AS4Message is successfully stored into the datastore", messagingContext.LogTag);

            var result = await StepResult.SuccessAsync(resultContext);

            if (!messagingContext.AS4Message.IsEmpty)
            {
                var entry = JournalLogEntry.CreateFrom(
                    messagingContext.AS4Message,
                    $"Saved message ({messagingContext.AS4Message.GetPrimaryMessageId()})");
                return await result.WithJournalAsync(entry);
            }

            return result;
        }

        _logger.LogError(resultContext.Exception, "{LogTag} The AS4Message is not stored correctly into the datastore", messagingContext.LogTag);
        return await StepResult.FailedAsync(resultContext);
    }

    private async Task<MessagingContext> InsertReceivedAS4MessageAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        var messageExchangePattern = messagingContext.Mode == MessagingContextMode.PullReceive
            ? MessageExchangePattern.Pull
            : MessageExchangePattern.Push;

        try
        {
            var as4Message = await _inMessageService.InsertAS4MessageAsync(
                messagingContext.AS4Message!,
                messagingContext.ReceivedMessage!,
                messagingContext.SendingPMode,
                messageExchangePattern, cancellation);

            messagingContext.ModifyContext(as4Message);

            return messagingContext;
        }
        catch (Exception ex)
        {
            return new MessagingContext(ex);
        }
    }
}
