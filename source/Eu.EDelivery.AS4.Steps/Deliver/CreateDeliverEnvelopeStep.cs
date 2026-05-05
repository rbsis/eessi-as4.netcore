using System.ComponentModel;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Microsoft.Extensions.Logging;
using MessageProperty = Eu.EDelivery.AS4.Model.Common.MessageProperty;
using Party = Eu.EDelivery.AS4.Model.Common.Party;
using PartyId = Eu.EDelivery.AS4.Model.Common.PartyId;

namespace Eu.EDelivery.AS4.Steps.Deliver;

/// <summary>
/// <see cref="IStep" /> implementation to create a <see cref="DeliverMessage" />.
/// </summary>
[Description("Step that creates a deliver message")]
[Info("Create deliver message")]
public class CreateDeliverEnvelopeStep : IStep
{
    private readonly ILogger<CreateDeliverEnvelopeStep> _logger;

    public CreateDeliverEnvelopeStep(ILogger<CreateDeliverEnvelopeStep> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Execute the step for a given <paramref name="messagingContext" />.
    /// </summary>
    /// <param name="messagingContext">Message used during the step execution.</param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        if (messagingContext.AS4Message == null)
        {
            throw new InvalidOperationException(
                $"{nameof(CreateDeliverEnvelopeStep)} requires an AS4Message to create a DeliverMessage from "
                + "but no AS4Message is present in the MessagingContext");
        }

        if (messagingContext.ReceivingPMode == null)
        {
            throw new InvalidOperationException(
                $"{nameof(CreateDeliverEnvelopeStep)} requires a ReceivingPMode which the DeliverMessage will reference to "
                + "but no SendingPMode is present in the MessagingContext");
        }

        if (!messagingContext.AS4Message.HasUserMessage)
        {
            throw new InvalidOperationException(
                $"{nameof(CreateDeliverEnvelopeStep)} requires an AS4Message with at least one UserMessage to create a DeliverMessage");
        }

        var as4Message = messagingContext.AS4Message;
        var toBeDeliveredUserMessage = as4Message.FirstUserMessage ??
            throw new InvalidOperationException("No UserMessage can be found in stored record for delivering");

        var toBeUploadedAttachments = as4Message.Attachments
            .Where(a => a.MatchesAny(toBeDeliveredUserMessage.PayloadInfo))
            .ToArray();

        var deliverMessage = CreateDeliverMessage(
            toBeDeliveredUserMessage,
            messagingContext.ReceivingPMode);

        _logger.LogInformation("(Deliver) Created DeliverMessage from (first) UserMessage {MessageId}", as4Message.FirstUserMessage!.MessageId);

        var envelope = new DeliverMessageEnvelope(
            message: deliverMessage,
            contentType: "application/xml",
            attachments: toBeUploadedAttachments);

        messagingContext.ModifyContext(envelope);

        return await StepResult.SuccessAsync(messagingContext);
    }

    private static DeliverMessage CreateDeliverMessage(UserMessage userMessage, ReceivingProcessingMode receivingPMode) => new()
    {
        MessageInfo =
            {
                MessageId = userMessage.MessageId,
                RefToMessageId = userMessage.RefToMessageId,
                Mpc = userMessage.Mpc
            },
        CollaborationInfo =
            {
                Action = userMessage.CollaborationInfo.Action,
                ConversationId = userMessage.CollaborationInfo.ConversationId,
                AgreementRef = receivingPMode != null ? new() { PModeId = receivingPMode.Id } : null,
                Service = new()
                {
                    Type = userMessage.CollaborationInfo.Service.Type.GetOrElse(() => null!),
                    Value = userMessage.CollaborationInfo.Service.Value
                }
            },
        PartyInfo =
            {
                FromParty = CreateDeliverParty(userMessage.Sender),
                ToParty = CreateDeliverParty(userMessage.Receiver)
            },
        MessageProperties = [.. userMessage.MessageProperties.Select(CreateDeliverMessageProperty)],
        Payloads = [.. userMessage.PayloadInfo.Select(CreateDeliverPayload)]
    };

    private static Party CreateDeliverParty(Model.Core.Party p) => new()
    {
        Role = p.Role,
        PartyIds = [.. p.PartyIds.Select(id => new PartyId(id.Id, id.Type.GetOrElse(() => null!)))]
    };

    private static MessageProperty CreateDeliverMessageProperty(Model.Core.MessageProperty p) =>
        new(p.Name, p.Value) { Type = p.Type };

    private static Payload CreateDeliverPayload(PartInfo part) => new()
    {
        Id = part.Href,
        MimeType = part.HasMimeType ? part.MimeType : null,
        PayloadProperties = [.. part.Properties.Select(p => new PayloadProperty(p.Key, p.Value))]
    };
}
