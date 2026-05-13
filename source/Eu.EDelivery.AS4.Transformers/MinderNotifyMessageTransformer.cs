using System.Diagnostics.CodeAnalysis;
using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.Notify;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;
using Microsoft.Extensions.Logging;
using AgreementReference = Eu.EDelivery.AS4.Model.Core.AgreementReference;
using CollaborationInfo = Eu.EDelivery.AS4.Model.Core.CollaborationInfo;
using MessageProperty = Eu.EDelivery.AS4.Model.Core.MessageProperty;
using Service = Eu.EDelivery.AS4.Model.Core.Service;

namespace Eu.EDelivery.AS4.Transformers;

[ExcludeFromCodeCoverage]
public abstract class MinderNotifyMessageTransformer : ITransformer
{
    protected abstract string MinderUriPrefix { get; }

    private readonly ILogger<MinderNotifyMessageTransformer> _logger;
    private readonly IDatastoreRepository _repository;
    private readonly IIdentifierFactory _identifierFactory;
    private readonly ISerializerProvider _serializerProvider;
    private readonly IAS4MessageBodyStore _bodyStore;
    private readonly AS4MessageTransformer _transformer;

    protected MinderNotifyMessageTransformer(
        ILogger<MinderNotifyMessageTransformer> logger,
        IDatastoreRepository repository,
        IIdentifierFactory identifierFactory,
        ISerializerProvider serializerProvider,
        IAS4MessageBodyStore bodyStore,
        AS4MessageTransformer transformer)
    {
        _logger = logger;
        _repository = repository;
        _identifierFactory = identifierFactory;
        _serializerProvider = serializerProvider;
        _bodyStore = bodyStore;
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
        var receivedEntityMessage = message as ReceivedEntityMessage
            ?? throw new NotSupportedException($"Minder Notify Transformer only supports transforming instances of type {typeof(ReceivedEntityMessage)}");

        var context = await _transformer.TransformAsync(message, cancellation);
        if (context.AS4Message is null)
        {
            throw new InvalidOperationException("AS4Message not found");
        }

        var notifyMessage =
            await CreateNotifyMessageEnvelopeAsync(
                context.AS4Message,
                receivedEntityMessage.Entity.GetType(),
                cancellation);

        context.ModifyContext(notifyMessage);

        return context;
    }

    internal async Task<NotifyMessageEnvelope> CreateNotifyMessageEnvelopeAsync(
        AS4Message as4Message,
        Type receivedEntityType,
        CancellationToken cancellation)
    {
        var signalMessage = as4Message.FirstSignalMessage;
        if (signalMessage is not null)
        {
            _logger.LogInformation("Minder Create Notify Message as {SignalMessage}", signalMessage.GetType().Name);
        }
        else
        {
            _logger.LogWarning("{MessageId} AS4Message does not contain a primary SignalMessage", as4Message.FirstUserMessage?.MessageId);
        }

        return await CreateMinderNotifyMessageEnvelope(as4Message, receivedEntityType, cancellation);
    }

    private async Task<NotifyMessageEnvelope> CreateMinderNotifyMessageEnvelope(
        AS4Message as4Message, Type receivedEntityMessageType,
        CancellationToken cancellation)
    {
        var userMessage = as4Message.FirstUserMessage;
        var signalMessage = as4Message.FirstSignalMessage;

        if (userMessage is null && signalMessage is not null)
        {
            userMessage = await RetrieveRelatedUserMessage(signalMessage, cancellation);
        }

        if (userMessage is null)
        {
            _logger.LogWarning("The related usermessage for the received signalmessage could not be found");
            userMessage = new UserMessage(_identifierFactory.Create());
        }

        var minderUserMessage = CreateUserMessageFromMinderProperties(userMessage, signalMessage);

        var notifyMessage =
            AS4MessageToNotifyMessageMapper.Convert(
                as4Message.FirstSignalMessage,
                receivedEntityMessageType,
                as4Message.EnvelopeDocument ?? AS4XmlSerializer.ToSoapEnvelopeDocument(as4Message));

        // The NotifyMessage that Minder expects, is an AS4Message which contains the specific UserMessage.
        var msg = AS4Message.Create(minderUserMessage, new SendingProcessingMode());
        var serializer = _serializerProvider.Get(msg.ContentType);

        byte[] content;

        using (var memoryStream = new MemoryStream())
        {
            await serializer.SerializeAsync(msg, memoryStream, cancellation);
            content = memoryStream.ToArray();
        }

        return new NotifyMessageEnvelope(notifyMessage.MessageInfo, notifyMessage.StatusInfo.Status, content, msg.ContentType, receivedEntityMessageType);
    }

    private async Task<UserMessage?> RetrieveRelatedUserMessage(
        SignalMessage signalMessage,
        CancellationToken cancellation)
    {
        if (signalMessage.RefToMessageId is null)
        {
            return null;
        }

        var ent = _repository.GetInOrOutMessageEntityFor(signalMessage.RefToMessageId);
        if (ent == null || ent.ContentType == null)
        {
            return null;

        }
        using var stream = await _bodyStore.LoadMessageBodyAsync(ent.MessageLocation, cancellation);
        if (stream == null)
        {
            return null;
        }

        stream.Position = 0;
        var s = _serializerProvider.Get(ent.ContentType);
        var result = await s.DeserializeAsync(stream, ent.ContentType, cancellation);
        return result?.UserMessages.FirstOrDefault(m => m.MessageId == signalMessage.RefToMessageId);
    }

    private UserMessage CreateUserMessageFromMinderProperties(UserMessage userMessage, SignalMessage? signalMessage)
    {
        var receiver =
            new Model.Core.Party(
                role: $"{MinderUriPrefix}/testdriver",
                partyId: new Model.Core.PartyId(id: "minder"));

        var collaboration = new CollaborationInfo(
            Maybe<AgreementReference>.Nothing,
            new Service(MinderUriPrefix),
            "Notify",
            CollaborationInfo.DefaultConversationId);

        var props = GetProperties(signalMessage);

        return new UserMessage(
            messageId: userMessage.MessageId,
            refToMessageId: !string.IsNullOrEmpty(signalMessage?.RefToMessageId) ? signalMessage.RefToMessageId : userMessage.RefToMessageId,
            mpc: userMessage.Mpc,
            timestamp: DateTimeOffset.Now,
            collaboration: collaboration,
            sender: userMessage.Sender,
            receiver: receiver,
            partInfos: userMessage.PayloadInfo,
            messageProperties: userMessage.MessageProperties.Concat(props));
    }

    private static IEnumerable<MessageProperty> GetProperties(SignalMessage? signalMessage)
    {
        if (signalMessage is null)
        {
            yield break;
        }
        if (!string.IsNullOrEmpty(signalMessage.RefToMessageId))
        {
            yield return new MessageProperty("RefToMessageId", signalMessage.RefToMessageId);
        }

        yield return new MessageProperty("SignalType", signalMessage.GetType().Name);
    }
}
