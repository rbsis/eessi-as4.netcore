using System.Diagnostics.CodeAnalysis;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Streaming;
using Microsoft.Extensions.Logging;
using MessageProperty = Eu.EDelivery.AS4.Model.Core.MessageProperty;

namespace Eu.EDelivery.AS4.Transformers.ConformanceTestTransformers;

[NotConfigurable]
[ExcludeFromCodeCoverage]
public class ConformanceTestingSubmitReceiveMessageTransformer : ITransformer
{
    private readonly ILogger<ConformanceTestingSubmitReceiveMessageTransformer> _logger;
    private readonly IConfig _config;
    private readonly AS4MessageTransformer _transformer;

    public ConformanceTestingSubmitReceiveMessageTransformer(
        ILogger<ConformanceTestingSubmitReceiveMessageTransformer> logger,
        IConfig config,
        AS4MessageTransformer transformer)
    {
        _logger = logger;
        _config = config;
        _transformer = transformer;
    }
    /// <summary>
    /// Configures the <see cref="ITransformer"/> implementation with specific user-defined properties.
    /// </summary>
    /// <param name="properties">The properties.</param>
    public void Configure(IDictionary<string, string> properties) { }

    /// <summary>
    /// Transform a given <see cref="ReceivedMessage"/> to a Canonical <see cref="MessagingContext"/> instance.
    /// </summary>
    /// <param name="message">Given message to transform.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> TransformAsync(ReceivedMessage message, CancellationToken cancellation)
    {
        // We receive an AS4Message from Minder, we should convert it to a SubmitMessage if the action is submit.
        // In any other case, we should just return a MessagingContext which contains the as4Message.
        var receivedStream = VirtualStream.Create();

        await message.UnderlyingStream.CopyToAsync(receivedStream, cancellation);
        receivedStream.Position = 0;

        var receivedMessage = new ReceivedMessage(
            receivedStream,
            message.ContentType,
            message.Origin,
            message.Length);

        try
        {
            var messagingContext = await _transformer.TransformAsync(receivedMessage, cancellation);
            if (messagingContext.AS4Message is null)
            {
                throw new InvalidMessageException("Messaging context must contain an AS4 Message");
            }

            if (messagingContext.AS4Message.FirstUserMessage?.CollaborationInfo?.Action?.Equals("Submit", StringComparison.OrdinalIgnoreCase) ?? false)
            {
                var as4Message = TransformMinderSubmitToAS4Message(messagingContext.AS4Message.FirstUserMessage, messagingContext.AS4Message.Attachments);
                messagingContext = new MessagingContext(as4Message, MessagingContextMode.Submit);

                AssignPModeToContext(messagingContext, "Submit");

                return messagingContext;
            }

            receivedStream.Position = 0;
            return new MessagingContext(messagingContext.AS4Message, receivedMessage, MessagingContextMode.Receive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transform failed");
            throw;
        }
    }

    private static AS4Message TransformMinderSubmitToAS4Message(UserMessage submitMessage, IEnumerable<Attachment> attachments)
    {
        var userMessage = new UserMessage(
            messageId: GetPropertyValue(submitMessage.MessageProperties, "MessageId"),
            refToMessageId: GetPropertyValue(submitMessage.MessageProperties, "RefToMessageId"),
            mpc: submitMessage.Mpc,
            timestamp: DateTimeOffset.Now,
            collaboration: GetCollaborationFromProperties(submitMessage.MessageProperties),
            sender: GetSenderFromSender(submitMessage),
            receiver: GetReceiverFromProperties(submitMessage),
            partInfos: submitMessage.PayloadInfo,
            messageProperties: WhiteListedMessageProperties(submitMessage.MessageProperties));

        var result = AS4Message.Create(userMessage);
        result.AddAttachments(attachments);

        return result;
    }

    private static CollaborationInfo GetCollaborationFromProperties(IEnumerable<MessageProperty> properties)
    {
        // AgreementRef must not be present in the AS4Message for minder.
        return new CollaborationInfo(
            Maybe<AgreementReference>.Nothing,
            new Service(GetPropertyValue(properties, "Service")),
            GetPropertyValue(properties, "Action"),
            GetPropertyValue(properties, "ConversationId"));
    }

    private static Party GetSenderFromSender(UserMessage submitMessage)
    {
        return new Party(
            role: GetPropertyValue(submitMessage.MessageProperties, "FromPartyRole"),
            partyId: new PartyId(
                id: GetPropertyValue(submitMessage.MessageProperties, "FromPartyId"),
                type: submitMessage.Sender.PartyIds.First().Type));
    }

    private static Party GetReceiverFromProperties(UserMessage submitMessage)
    {
        return new Party(
            role: GetPropertyValue(submitMessage.MessageProperties, "ToPartyRole"),
            partyId: new PartyId(
                id: GetPropertyValue(submitMessage.MessageProperties, "ToPartyId"),
                type: submitMessage.Receiver.PartyIds.First().Type));
    }

    private static IEnumerable<MessageProperty> WhiteListedMessageProperties(IEnumerable<MessageProperty> properties)
    {
        string[] whiteList = ["originalSender", "finalRecipient", "trackingIdentifier"];
        return properties.Where(p => whiteList.Contains(p.Name, StringComparer.OrdinalIgnoreCase));
    }

    private void AssignPModeToContext(MessagingContext context, string action)
    {
        // The PMode that must be used is defined in the CollaborationInfo.Service property.
        var pmode = _config.GetSendingPMode(action);
        context.SendingPMode = pmode;
    }

    private static string GetPropertyValue(IEnumerable<MessageProperty> properties, string propertyName)
    {
        return properties.FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))?.Value
            ?? throw new InvalidOperationException($"Property {propertyName} not found");
    }
}
