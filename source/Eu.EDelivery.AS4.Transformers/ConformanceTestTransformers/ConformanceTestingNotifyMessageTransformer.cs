using System.Diagnostics.CodeAnalysis;
using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Transformers.ConformanceTestTransformers;

[NotConfigurable]
[ExcludeFromCodeCoverage]
public class ConformanceTestingNotifyMessageTransformer : MinderNotifyMessageTransformer
{
    protected override string MinderUriPrefix => "http://www.esens.eu/as4/conformancetest";

    public ConformanceTestingNotifyMessageTransformer(
        ILogger<ConformanceTestingNotifyMessageTransformer> logger,
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
