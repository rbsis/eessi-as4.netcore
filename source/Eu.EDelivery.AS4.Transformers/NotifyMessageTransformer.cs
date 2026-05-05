using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.Notify;
using Eu.EDelivery.AS4.Serialization;

namespace Eu.EDelivery.AS4.Transformers;

public class NotifyMessageTransformer : ITransformer
{
    private readonly IIdentifierFactory _identifierFactory;
    private readonly AS4MessageTransformer _transformer;

    public NotifyMessageTransformer(IIdentifierFactory identifierFactory, AS4MessageTransformer transformer)
    {
        _identifierFactory = identifierFactory;
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
        ArgumentNullException.ThrowIfNull(message);

        if (message is not ReceivedEntityMessage receivedMessage)
        {
            throw new NotSupportedException(
                $"Incoming message stream from {message.Origin} that must be transformed should be of type {nameof(ReceivedEntityMessage)}");
        }

        if (receivedMessage.Entity is ExceptionEntity ex)
        {
            var ebmsMessageId = _identifierFactory.Create();
            var error = Error.FromErrorResult(
                ebmsMessageId,
                ex.EbmsRefToMessageId,
                new ErrorResult(ex.Exception, ErrorAlias.Other));

            var notifyEnvelope = await CreateNotifyMessageEnvelopeAsync(
                AS4Message.Create(error),
                ebmsMessageId,
                ex.GetType(),
                cancellation);

            return new MessagingContext(notifyEnvelope, receivedMessage);
        }

        if (receivedMessage.Entity is MessageEntity me)
        {
            var ctx = await _transformer.TransformAsync(receivedMessage, cancellation);
            if (ctx.AS4Message is null)
            {
                throw new InvalidOperationException("AS4Message not found");
            }

            // Normally the message shouldn't have any attachments
            // but to be sure we should dispose them since we don't need attachments for notifying.
            ctx.AS4Message.CloseAttachments();

            var notifyEnvelope = await CreateNotifyMessageEnvelopeAsync(
                ctx.AS4Message,
                me.EbmsMessageId,
                me.GetType(),
                cancellation);

            ctx.ModifyContext(notifyEnvelope, receivedMessage.Entity.Id);

            return ctx;
        }

        throw new InvalidOperationException();
    }

    protected virtual async Task<NotifyMessageEnvelope> CreateNotifyMessageEnvelopeAsync(
        AS4Message as4Message,
        string receivedEntityMessageId,
        Type receivedEntityType,
        CancellationToken cancellation)
    {
        var tobeNotifiedSignal =
            as4Message.SignalMessages.FirstOrDefault(s => s.MessageId == receivedEntityMessageId);

        var notifyMessage =
            AS4MessageToNotifyMessageMapper.Convert(
                tobeNotifiedSignal,
                receivedEntityType,
                as4Message.EnvelopeDocument ?? AS4XmlSerializer.ToSoapEnvelopeDocument(as4Message));

        var serialized = await AS4XmlSerializer.ToStringAsync(notifyMessage, cancellation);

        return new NotifyMessageEnvelope(
            notifyMessage.MessageInfo,
            notifyMessage.StatusInfo.Status,
            System.Text.Encoding.UTF8.GetBytes(serialized),
            "application/xml",
            receivedEntityType);
    }
}
