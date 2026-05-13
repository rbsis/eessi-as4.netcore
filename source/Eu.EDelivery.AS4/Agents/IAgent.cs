using Microsoft.Extensions.Hosting;

namespace Eu.EDelivery.AS4.Agents;

/// <summary>
/// Interface to provide an extendable Agent
/// </summary>
public interface IAgent : IHostedService
{
    /// <summary>
    /// Gets the agent configuration.
    /// </summary>
    /// <value>The agent configuration.</value>
    AgentConfig AgentConfig { get; }
}
