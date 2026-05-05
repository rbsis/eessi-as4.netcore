using System.ComponentModel;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Services;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Submit;

/// <summary>
/// Describes how the AS4 UserMessage is stored in the message store,
/// in order to hand over to the Send Agents.
/// </summary>
[Info("Store AS4 message")]
[Description(
    "Stores the AS4 Message that has been created for the received SubmitMessage " +
    "so that it can be processed (signed, encrypted, …) afterwards.")]
public class StoreAS4MessageStep : IStep
{
    private readonly ILogger<StoreAS4MessageStep> _logger;

    // TODO: this class should be reviewed IMHO.  We should not save AS4Messages, but we should
    // save the MessagePart in the OutMessage table.  Each MessagePart has its own messagebody.
    // Right now, the MessageBody is the complete AS4Message; every OutMessage refers to that same messagebody which 
    // is not correct.
    // At this stage, there should be no AS4-message in my opinion, only UserMessages and SignalMessages.
    private readonly IOutMessageService _outMessageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreAS4MessageStep" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="outMessageService">The repository.</param>
    public StoreAS4MessageStep(
        ILogger<StoreAS4MessageStep> logger,
        IOutMessageService outMessageService)
    {
        _logger = logger;
        _outMessageService = outMessageService;
    }

    /// <summary>
    /// Execute the step for a given <paramref name="messagingContext"/>.
    /// </summary>
    /// <param name="messagingContext">The Message used during the step execution.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        if (messagingContext?.AS4Message == null)
        {
            throw new InvalidOperationException(
                $"{nameof(StoreAS4MessageStep)} requires an AS4Message to save but no AS4Message is present in the MessagingContext");
        }

        _logger.LogTrace("Storing the AS4Message with Operation=ToBeProcessed...");
        try
        {
            _outMessageService.InsertAS4Message(
                messagingContext.AS4Message,
                messagingContext.SendingPMode,
                messagingContext.ReceivingPMode);
        }
        catch
        {
            messagingContext.ErrorResult = new ErrorResult(
                "Unable to store the received message due to an exception occured during the saving operation",
                ErrorAlias.Other);
            throw;
        }

        _logger.LogTrace("Stored the AS4Message with Operation=ToBeProcesed");
        return await StepResult.SuccessAsync(messagingContext);
    }
}
