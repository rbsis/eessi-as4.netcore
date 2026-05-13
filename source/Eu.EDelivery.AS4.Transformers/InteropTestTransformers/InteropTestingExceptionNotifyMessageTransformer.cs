using System.Diagnostics.CodeAnalysis;
using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Notify;

namespace Eu.EDelivery.AS4.Transformers.InteropTestTransformers;

[NotConfigurable]
[ExcludeFromCodeCoverage]
public class InteropTestingExceptionNotifyMessageTransformer : NotifyMessageTransformer
{
    private readonly InteropTestingNotifyMessageTransformer _notifyTransformer;

    public InteropTestingExceptionNotifyMessageTransformer(
        IIdentifierFactory identifierFactory,
        AS4MessageTransformer transformer,
        InteropTestingNotifyMessageTransformer notifyTransformer) :
            base(identifierFactory, transformer)
    {
        _notifyTransformer = notifyTransformer;
    }

    protected override async Task<NotifyMessageEnvelope> CreateNotifyMessageEnvelopeAsync(
        AS4Message as4Message,
        string receivedEntityMessageId,
        Type receivedEntityType,
        CancellationToken cancellation) =>
            await _notifyTransformer.CreateNotifyMessageEnvelopeAsync(as4Message, receivedEntityType, cancellation);
}
