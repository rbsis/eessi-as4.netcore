using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Transformers;

public class DeliverMessageTransformer : ITransformer
{
    private readonly ILogger<DeliverMessageTransformer> _logger;
    private readonly AS4MessageTransformer _transformer;

    public DeliverMessageTransformer(ILogger<DeliverMessageTransformer> logger, AS4MessageTransformer transformer)
    {
        _logger = logger;
        _transformer = transformer;
    }

    /// <summary>
    /// Configures the <see cref="ITransformer"/> implementation with specific user-defined properties.
    /// </summary>
    /// <param name="properties">The properties.</param>
    public void Configure(IDictionary<string, string> properties) { }

    /// <summary>
    /// Transform a given <see cref="ReceivedMessage" /> to a Canonical <see cref="MessagingContext" /> instance.
    /// </summary>
    /// <param name="message">Given message to transform.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> TransformAsync(ReceivedMessage message, CancellationToken cancellation)
    {
        if (message is not ReceivedEntityMessage entityMessage || entityMessage.Entity is not MessageEntity me)
        {
            throw new InvalidDataException(
                $"The message that must be transformed should be of type {nameof(ReceivedEntityMessage)} with a {nameof(MessageEntity)} as Entity");
        }

        var context = await _transformer.TransformAsync(entityMessage, cancellation);
        var as4Message = context.AS4Message;

        var toBeDeliveredUserMessage = as4Message?.UserMessages.FirstOrDefault(u => u.MessageId == me.EbmsMessageId)
            ?? throw new InvalidOperationException($"No UserMessage {me.EbmsMessageId} can be found in stored record for delivering");

        var toBeUploadedAttachments = as4Message.Attachments
                .Where(a => a.MatchesAny(toBeDeliveredUserMessage.PayloadInfo))
                .ToArray();

        var deliverMessage = CreateDeliverMessage(
            toBeDeliveredUserMessage,
            toBeUploadedAttachments,
            context.ReceivingPMode);

        _logger.LogInformation("(Deliver) Created DeliverMessage from (first) UserMessage {MessageId}", as4Message.FirstUserMessage?.MessageId);
        var envelope = new DeliverMessageEnvelope(
            message: deliverMessage,
            contentType: "application/xml",
            attachments: toBeUploadedAttachments);

        context.ModifyContext(envelope);
        return context;
    }

    private static DeliverMessage CreateDeliverMessage(
        UserMessage user,
        IEnumerable<Attachment> attachments,
        ReceivingProcessingMode? receivingPMode)
    {
        if (!attachments.All(a => a.MatchesAny(user.PayloadInfo)))
        {
            throw new InvalidOperationException(
                "Not all attachments in AS4Message references to an <PartInfo/> element");
        }

        return new DeliverMessage
        {
            MessageInfo =
            {
                MessageId = user.MessageId,
                RefToMessageId = user.RefToMessageId,
                Mpc = user.Mpc
            },
            CollaborationInfo =
            {
                Action = user.CollaborationInfo.Action,
                ConversationId = user.CollaborationInfo.ConversationId,
                AgreementRef = receivingPMode != null ? new() { PModeId = receivingPMode.Id } : null,
                Service = new()
                {
                    Type = user.CollaborationInfo.Service.Type.GetOrElse(() => null!),
                    Value = user.CollaborationInfo.Service.Value
                }
            },
            PartyInfo =
            {
                FromParty = CreateDeliverParty(user.Sender),
                ToParty = CreateDeliverParty(user.Receiver)
            },
            MessageProperties = [.. user.MessageProperties.Select(CreateDeliverMessageProperty)],
            Payloads = [.. user.PayloadInfo.Select(CreateDeliverPayload)]
        };
    }

    private static Model.Common.Party CreateDeliverParty(Model.Core.Party p) => new()
    {
        Role = p.Role,
        PartyIds = [.. p.PartyIds.Select(id => new Model.Common.PartyId(id.Id, id.Type.GetOrElse(() => null!)))]
    };

    private static Model.Common.MessageProperty CreateDeliverMessageProperty(Model.Core.MessageProperty p) => new(p.Name, p.Value)
    {
        Type = p.Type
    };

    private static Payload CreateDeliverPayload(PartInfo part) => new()
    {
        Id = part.Href,
        MimeType = part.HasMimeType ? part.MimeType : null,
        PayloadProperties = [.. part.Properties.Select(p => new PayloadProperty(p.Key, p.Value))]
    };
}
