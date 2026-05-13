using System.Diagnostics.CodeAnalysis;
using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Transformers.InteropTestTransformers;

[NotConfigurable]
[ExcludeFromCodeCoverage]
public class InteropTestingNotifyMessageTransformer : MinderNotifyMessageTransformer
{
    protected override string MinderUriPrefix => "http://www.esens.eu/as4/interoptest";

    public InteropTestingNotifyMessageTransformer(
        ILogger<InteropTestingNotifyMessageTransformer> logger,
        IDatastoreRepository repository,
        IIdentifierFactory identifierFactory,
        ISerializerProvider serializerProvider,
        IAS4MessageBodyStore bodyStore,
        AS4MessageTransformer transformer) : base(
            logger,
            repository,
            identifierFactory,
            serializerProvider,
            bodyStore,
            transformer)
    { }
}
