using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Transformers;

public interface ITransformerBuilder
{
    ITransformer BuildFromConfig(Transformer config);
}
