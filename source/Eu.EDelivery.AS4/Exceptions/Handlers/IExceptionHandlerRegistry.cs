using Eu.EDelivery.AS4.Agents;

namespace Eu.EDelivery.AS4.Exceptions.Handlers;

public interface IExceptionHandlerRegistry
{
    IAgentExceptionHandler GetHandler(AgentType type);
}
