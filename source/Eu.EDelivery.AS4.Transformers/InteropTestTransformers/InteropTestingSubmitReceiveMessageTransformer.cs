using System.Diagnostics.CodeAnalysis;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using static Eu.EDelivery.AS4.Constants.Namespaces;

namespace Eu.EDelivery.AS4.Transformers.InteropTestTransformers;

[ExcludeFromCodeCoverage]
public class InteropTestingSubmitReceiveMessageTransformer : ITransformer
{
    private readonly IConfig _config;
    private readonly AS4MessageTransformer _transformer;

    public InteropTestingSubmitReceiveMessageTransformer(IConfig config, AS4MessageTransformer transformer)
    {
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
        // In any other case, we should just return an MessagingContext which contains the as4Message.
        var messagingContext = await _transformer.TransformAsync(message, cancellation);

        var as4Message = messagingContext.AS4Message
            ?? throw new InvalidOperationException("AS4Message not found");

        if (as4Message.FirstUserMessage?.CollaborationInfo?.Action?.Equals("Submit", StringComparison.OrdinalIgnoreCase) ?? false)
        {
            var properties = as4Message.FirstUserMessage.MessageProperties;

            var transformed = TransformUserMessage(as4Message.FirstUserMessage, properties);
            as4Message.UpdateMessageUnit(as4Message.FirstUserMessage, transformed);

            messagingContext = new MessagingContext(as4Message, MessagingContextMode.Submit);

            AssignPModeToContext(as4Message.FirstUserMessage, messagingContext);

            return messagingContext;
        }

        return new MessagingContext(as4Message, MessagingContextMode.Receive);
    }

    private void AssignPModeToContext(UserMessage userMessage, MessagingContext message)
    {
        // The PMode that should be used can be determind by concatenating several items to create the PMode ID
        // - CollaborationInfo.Action
        // - ToParty
        var pmodeKey = $"{userMessage.CollaborationInfo.Action}_FROM_{userMessage.Sender.PartyIds.First().Id}_TO_{userMessage.Receiver.PartyIds.First().Id}";

        // The PMode that must be used is defined in the CollaborationInfo.Service property.
        var pmode = _config.GetSendingPMode(pmodeKey);

        message.SendingPMode = pmode;
    }

    private static UserMessage TransformUserMessage(UserMessage userMessage, IEnumerable<MessageProperty> properties)
    {
        return new UserMessage(
            GetMandatoryPropertyValue(properties, "MessageId"),
            GetPropertyValue(properties, "RefToMessageId"),
            GetCollaborationFromProperties(properties),
            GetSenderFromproperties(properties),
            GetReceiverFromProperties(properties),
            [],
            WhiteListedMessageProperties(userMessage));
    }

    private static IEnumerable<MessageProperty> WhiteListedMessageProperties(UserMessage userMessage)
    {
        string[] whiteList = ["originalSender", "finalRecipient", "trackingIdentifier", "TA_Id"];
        return userMessage.MessageProperties
                   .Where(p => whiteList.Contains(p.Name, StringComparer.OrdinalIgnoreCase));
    }

    private static CollaborationInfo GetCollaborationFromProperties(IEnumerable<MessageProperty> properties) => new(
        // AgreementRef must not be present in the AS4Message for minder.
        Maybe<AgreementReference>.Nothing,
        new Service(GetMandatoryPropertyValue(properties, "Service")),
        GetMandatoryPropertyValue(properties, "Action"),
        GetMandatoryPropertyValue(properties, "ConversationId"));

    private static Party GetReceiverFromProperties(IEnumerable<MessageProperty> properties)
    {
        return new Party(
            role: GetPropertyValue(properties, "ToPartyRole") ?? EbmsDefaultRole,
            partyId: new PartyId(
                id: GetMandatoryPropertyValue(properties, "ToPartyId"),
                type: GetPropertyValue(properties, "ToPartyType")));
    }

    private static Party GetSenderFromproperties(IEnumerable<MessageProperty> properties)
    {
        return new Party(
            role: GetPropertyValue(properties, "FromPartyRole") ?? EbmsDefaultRole,
            partyId: new PartyId(
                id: GetMandatoryPropertyValue(properties, "FromPartyId"),
                type: GetPropertyValue(properties, "FromPartyType")));
    }

    private static string? GetPropertyValue(IEnumerable<MessageProperty> properties, string propertyName) =>
        properties.FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string GetMandatoryPropertyValue(IEnumerable<MessageProperty> properties, string propertyName) =>
        GetPropertyValue(properties, propertyName) ?? throw new InvalidOperationException($"Mandatory property {propertyName} not found");
}
