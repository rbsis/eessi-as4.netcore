using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Steps;

public interface IDefaultAgentStepRegistry
{
    StepConfiguration GetDefaultStepConfiguration(AgentType agentType);
}
