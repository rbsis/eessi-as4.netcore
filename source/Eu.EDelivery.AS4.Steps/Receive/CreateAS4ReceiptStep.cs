using System.ComponentModel;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Receive;

/// <summary>
/// Describes how the AS4 Receipt must be created
/// </summary>
[Info("Create a Receipt message")]
[Description("Create an AS4 Receipt message to inform the sender that the received AS4 Message has been processed correctly")]
public class CreateAS4ReceiptStep : IStep
{
    private readonly ILogger<CreateAS4ReceiptStep> _logger;
    private readonly IIdentifierFactory _identifierFactory;

    public CreateAS4ReceiptStep(ILogger<CreateAS4ReceiptStep> logger, IIdentifierFactory identifierFactory)
    {
        _logger = logger;
        _identifierFactory = identifierFactory;
    }

    /// <summary>
    /// It is only executed when the external message (received) is an AS4 UserMessage
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(messagingContext);

        if (messagingContext.AS4Message == null)
        {
            throw new InvalidOperationException(
                $"{nameof(CreateAS4ReceiptStep)} requires an AS4Message to create ebMS Receipts but no AS4Message is present in the MessagingContext");
        }

        var receiptSigning = messagingContext.ReceivingPMode?.ReplyHandling?.ResponseSigning?.IsEnabled ?? false;
        var useNRRFormat = messagingContext.ReceivingPMode?.ReplyHandling?.ReceiptHandling?.UseNRRFormat ?? false;

        if (!receiptSigning && useNRRFormat)
        {
            _logger.LogError(
                "Cannot create Non-Repudiation Receipts that aren\'t signed, please change either the "
                + "ReceivingPMode {PModeId} ReplyHandling.ReceiptHandling.UseNRRFormat or the ReplyHandling.ResponseSigning",
                messagingContext.ReceivingPMode!.Id);

            messagingContext.ErrorResult = new ErrorResult(
                "Cannot create Non-Repudiation Receipts that aren't signed",
                ErrorAlias.InvalidReceipt);

            return await StepResult.FailedAsync(messagingContext);
        }

        var receivedMessage = messagingContext.AS4Message;
        var receiptMessage = AS4Message.Empty;
        receiptMessage.SigningId = receivedMessage.SigningId;

        foreach (var userMessage in receivedMessage.UserMessages)
        {
            var receipt = CreateReferencedReceipt(userMessage, receivedMessage, messagingContext.ReceivingPMode);
            receiptMessage.AddMessageUnit(receipt);
        }

        if (_logger.IsEnabled(LogLevel.Error) && receiptMessage.MessageUnits.Any())
        {
            _logger.LogInformation("{LogTag} {Count} Receipt message(s) has been created for received AS4 UserMessages",
                messagingContext.LogTag,
                receiptMessage.MessageUnits.Count());
        }

        messagingContext.ModifyContext(receiptMessage);
        return await StepResult.SuccessAsync(messagingContext);
    }

    private Receipt CreateReferencedReceipt(
        UserMessage userMessage,
        AS4Message received,
        ReceivingProcessingMode? receivingPMode)
    {
        if (receivingPMode == null)
        {
            return Receipt.CreateFor(
                _identifierFactory.Create(),
                userMessage,
                received.IsMultiHopMessage);
        }

        var useNRRFormat = receivingPMode.ReplyHandling.ReceiptHandling.UseNRRFormat;
        if (useNRRFormat && !received.IsSigned)
        {
            _logger.LogWarning(
                "ReceivingPMode {PModeId} is configured to reply with Non-Repudation Receipts, "
                + "but incoming UserMessage {MessageId} isn\'t signed. "
                + "This means that the Receipt cannot be created as a Non-Repudiation Receipt "
                + "but in a Receipt with the referenced UserMessage embedded instead",
                receivingPMode.Id,
                userMessage.MessageId);
        }
        else if (!useNRRFormat)
        {
            _logger.LogTrace(
                "ReceivingPMode {PModeId} is configured to not use the Non-Repudiation format."
                + "This means the original UserMessage {MessageId} will be included in the Receipt",
                receivingPMode.Id,
                userMessage.MessageId);
        }

        if (received.IsMultiHopMessage)
        {
            _logger.LogTrace("Because the received UserMessage {MessageId} has been sent via MultiHop, the Receipt will be send with MultiHop as well",
                userMessage.MessageId);
        }

        if (useNRRFormat && received.IsSigned)
        {
            _logger.LogTrace("ReceivingPMode {PModeId} is configured to use Non-Repudiation for Receipt Creation", receivingPMode.Id);
            return Receipt.CreateFor(
                _identifierFactory.Create(),
                userMessage,
                received.SecurityHeader!,
                received.IsMultiHopMessage);
        }

        return Receipt.CreateFor(
            _identifierFactory.Create(),
            userMessage,
            received.IsMultiHopMessage);
    }
}
