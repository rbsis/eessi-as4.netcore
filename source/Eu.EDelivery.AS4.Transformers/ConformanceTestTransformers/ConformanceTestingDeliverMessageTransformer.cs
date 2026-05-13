using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Serialization;
using static Eu.EDelivery.AS4.Constants.Namespaces;
using MessageProperty = Eu.EDelivery.AS4.Model.Core.MessageProperty;
using Party = Eu.EDelivery.AS4.Model.Core.Party;
using PartyId = Eu.EDelivery.AS4.Model.Core.PartyId;
using Service = Eu.EDelivery.AS4.Model.Core.Service;

namespace Eu.EDelivery.AS4.Transformers.ConformanceTestTransformers;

[NotConfigurable]
public class ConformanceTestingDeliverMessageTransformer : ITransformer
{
    private string _uriPrefix;

    private readonly ISerializerProvider _serializerProvider;
    private readonly AS4MessageTransformer _transformer;

    public ConformanceTestingDeliverMessageTransformer(ISerializerProvider serializerProvider, AS4MessageTransformer transformer)
    {
        _serializerProvider = serializerProvider;
        _transformer = transformer;
        _uriPrefix = string.Empty;
    }

    /// <summary>
    /// Configures the <see cref="ITransformer"/> implementation with specific user-defined properties.
    /// </summary>
    /// <param name="properties">The properties.</param>
    public void Configure(IDictionary<string, string> properties)
    {
        _uriPrefix = properties.ReadMandatoryProperty("Uri");
    }

    /// <summary>
    /// Transform a given <see cref="ReceivedMessage"/> to a Canonical <see cref="MessagingContext"/> instance.
    /// </summary>
    /// <param name="message">Given message to transform.</param>
    /// 
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> TransformAsync(ReceivedMessage message, CancellationToken cancellation)
    {
        if (message is not ReceivedEntityMessage)
        {
            throw new NotSupportedException(
                $"Minder Deliver Transformer only supports transforming instances of type {typeof(ReceivedEntityMessage)}");
        }

        var context = await _transformer.TransformAsync(message, cancellation);

        var includeAttachments = true;
        var collaborationInfo = context.ReceivingPMode?.MessagePackaging?.CollaborationInfo;

        if (collaborationInfo != null &&
            (collaborationInfo.Action?.Equals("ACT_SIMPLE_ONEWAY_SIZE", StringComparison.OrdinalIgnoreCase) ?? false) &&
            (collaborationInfo.Service?.Value?.Equals("SRV_SIMPLE_ONEWAY_SIZE", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            includeAttachments = false;
        }

        var as4Message = context.AS4Message ?? throw new InvalidOperationException("AS4Message not found");
        var deliverMessage = CreateDeliverMessageEnvelope(as4Message, includeAttachments);
        context.ModifyContext(deliverMessage);

        return context;
    }

    private DeliverMessageEnvelope CreateDeliverMessageEnvelope(AS4Message as4Message, bool includeAttachments)
    {
        var userMessage = as4Message.FirstUserMessage ?? throw new InvalidOperationException("FirstUserMessage not found");
        var deliverMessage = CreateMinderDeliverMessage(userMessage);

        // The Minder Deliver Message should be an AS4-Message.
        var msg = AS4Message.Create(deliverMessage);

        if (includeAttachments)
        {
            msg.AddAttachments(as4Message.Attachments);
        }

        var content = SerializeAS4Message(msg);

        return new DeliverMessageEnvelope(
            message: new()
            {
                MessageInfo = new()
                {
                    MessageId = deliverMessage.MessageId,
                    RefToMessageId = deliverMessage.RefToMessageId
                },
                Payloads = msg.FirstUserMessage?.PayloadInfo.Select(CreateDeliverPayload).ToArray() ?? []
            },
            deliverMessage: content,
            contentType: msg.ContentType,
            attachments: as4Message.UserMessages.SelectMany(um => as4Message.Attachments.Where(a => a.MatchesAny(um.PayloadInfo))));
    }

    private UserMessage CreateMinderDeliverMessage(UserMessage userMessage)
    {
        // Party Information: sender is the receiver of the AS4Message that has been received.
        //                    receiver is minder.

        IEnumerable<MessageProperty> deliverProperties =
            new Dictionary<string, string?>
            {
                ["MessageId"] = userMessage.MessageId,
                ["RefToMessageId"] = userMessage.RefToMessageId,
                ["ConversationId"] = userMessage.CollaborationInfo.ConversationId,
                ["Service"] = userMessage.CollaborationInfo.Service.Value,
                ["Action"] = userMessage.CollaborationInfo.Action,
                ["FromPartyId"] = userMessage.Sender.PartyIds.First().Id,
                ["FromPartyRole"] = userMessage.Sender.Role,
                ["ToPartyId"] = userMessage.Receiver.PartyIds.First().Id,
                ["ToPartyRole"] = userMessage.Receiver.Role
            }
            .Where(kv => !string.IsNullOrEmpty(kv.Key) && !string.IsNullOrEmpty(kv.Value))
            .Select(kv => new MessageProperty(kv.Key, kv.Value!))
            .Concat(userMessage.MessageProperties.Where(p => p.Name.Equals("originalSender") || p.Name.Equals("finalRecipient")))
            .ToArray();

        return new(
            messageId: userMessage.MessageId,
            refToMessageId: userMessage.RefToMessageId,
            timestamp: userMessage.Timestamp,
            mpc: EbmsDefaultMpc,
            collaboration: new Model.Core.CollaborationInfo(
                agreement: Maybe<AgreementReference>.Nothing,
                service: new Service(_uriPrefix),
                action: "Deliver",
                conversationId: userMessage.CollaborationInfo.ConversationId),
            sender: new Party($"{_uriPrefix}/sut", userMessage.Receiver.PartyIds.FirstOrDefault() ?? new PartyId(EbmsDefaultTo)),
            receiver: new Party($"{_uriPrefix}/testdriver", new PartyId("minder")),
            partInfos: userMessage.PayloadInfo,
            messageProperties: deliverProperties);
    }

    private byte[] SerializeAS4Message(AS4Message msg)
    {
        var serializer = _serializerProvider.Get(msg.ContentType);

        using var memoryStream = new MemoryStream();
        serializer.Serialize(msg, memoryStream);
        return memoryStream.ToArray();
    }

    private static Payload CreateDeliverPayload(PartInfo part) => new()
    {
        Id = part.Href,
        MimeType = part.HasMimeType ? part.MimeType : null,
        PayloadProperties = [.. part.Properties.Select(p => new PayloadProperty(p.Key, p.Value))]
    };
}
