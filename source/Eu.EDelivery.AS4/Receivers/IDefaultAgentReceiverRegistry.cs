using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Receivers;

public interface IDefaultAgentReceiverRegistry
{
    /// <summary>
    /// Gets the default implementation of the <see cref="Receiver"/> for a given <paramref name="agentType"/>.
    /// </summary>
    /// <param name="agentType">Type of the agent.</param>
    /// <returns></returns>
    Receiver GetDefaultReceiver(AgentType agentType);
}
