using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Transformers;

public interface IDefaultAgentTransformerRegistry
{
    Transformer GetDefaultTransformer(AgentType agentType);
    IEnumerable<Transformer> GetOtherTransformers(AgentType agentType);
}
