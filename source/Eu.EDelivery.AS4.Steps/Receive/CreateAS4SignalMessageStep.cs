using System.ComponentModel;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Services;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Receive;

[Info("Create a signal message")]
[Description("Create an AS4 signal message to be send back to the original sender")]
public class CreateAS4SignalMessageStep : IStep
{
    private readonly ILogger<CreateAS4SignalMessageStep> _logger;
    private readonly IOutMessageService _outMessageService;
    private readonly IPiggyBackingService _piggyBackingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAS4SignalMessageStep"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="outMessageService"></param>
    /// <param name="piggyBackingService"></param>
    public CreateAS4SignalMessageStep(
        ILogger<CreateAS4SignalMessageStep> logger,
        IOutMessageService outMessageService,
        IPiggyBackingService piggyBackingService)
    {
        _logger = logger;
        _outMessageService = outMessageService;
        _piggyBackingService = piggyBackingService;
    }

    /// <summary>
    /// Start executing the Receipt Decorator
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(messagingContext);

        if (messagingContext.AS4Message == null || messagingContext.AS4Message.IsEmpty)
        {
            _logger.LogTrace("No SignalMessage available to send");
            return await StepResult.SuccessAsync(messagingContext);
        }

        InsertRespondSignalToDatastore(messagingContext);

        var replyPattern = messagingContext.ReceivingPMode?.ReplyHandling?.ReplyPattern;
        if (replyPattern == ReplyPattern.Callback || messagingContext.Mode == MessagingContextMode.PullReceive)
        {
            return CreateEmptySoapResult(messagingContext);
        }

        return CreateSignalResult(messagingContext);
    }

    private void InsertRespondSignalToDatastore(MessagingContext messagingContext)
    {
        var insertedMessageUnits =
            _outMessageService.InsertAS4Message(
                messagingContext.AS4Message!,
                messagingContext.SendingPMode,
                messagingContext.ReceivingPMode);

        var replyHandling = messagingContext.ReceivingPMode?.ReplyHandling;
        if (replyHandling is not null
            && replyHandling.ReplyPattern == ReplyPattern.PiggyBack
            && replyHandling.PiggyBackReliability is not null
            && replyHandling.PiggyBackReliability.IsEnabled)
        {
            _piggyBackingService.InsertRetryForPiggyBackedSignalMessages(
                insertedMessageUnits,
                replyHandling.PiggyBackReliability);

        }
    }

    private StepResult CreateEmptySoapResult(MessagingContext messagingContext)
    {
        _logger.LogDebug("Empty Accepted response will be send to requested party since signal will be sent async");

        return StepResult.Success(
            new MessagingContext(
                AS4Message.Create(messagingContext.SendingPMode),
                MessagingContextMode.Receive)
            {
                ReceivingPMode = messagingContext.ReceivingPMode
            });
    }

    private StepResult CreateSignalResult(MessagingContext context)
    {
        static string ConcatErrorDescriptions(Error e) =>
            e.ErrorLines != null ? string.Join(", ", e.ErrorLines.Select(er => er.Detail).Choose(x => x)) : string.Empty;

        if (_logger.IsEnabled(LogLevel.Information)
            && context.AS4Message is not null
            && context.AS4Message.PrimaryMessageUnit is not null)
        {
            var primaryMessageUnit = context.AS4Message.PrimaryMessageUnit;
            var errorDescriptions = primaryMessageUnit is Error error
                    ? ": " + ConcatErrorDescriptions(error)
                    : string.Empty;

            _logger.LogInformation(
                "({Mode}) <- response with {PrimaryMessageUnit} {MessageId} {ErrorDescriptions}",
                context.Mode,
                primaryMessageUnit.GetType().Name,
                primaryMessageUnit.MessageId,
                errorDescriptions);
        }

        return StepResult.Success(context);
    }
}
