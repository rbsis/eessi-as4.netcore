using Eu.EDelivery.AS4.Fe.Exceptions;
using Eu.EDelivery.AS4.Fe.Models;
using Eu.EDelivery.AS4.Fe.Modules;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Fe.Services;

/// <summary>
/// Service to manage settings.xml
/// </summary>
public interface IAs4SettingsService : IModular
{
    /// <summary>
    /// Saves the base settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SaveBaseSettingsAsync(BaseSettings settings, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the custom settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SaveCustomSettingsAsync(CustomSettings settings, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the database settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SaveDatabaseSettingsAsync(SettingsDatabase settings, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the submit settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SaveSubmitSettingsAsync(SettingsSubmit settings, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the pull send settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SavePullSendSettingsAsync(SettingsPullSend settings, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="getAgents">The get agents.</param>
    /// <param name="setAgents">The set agents.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="AlreadyExistsException">Indicates that an agent with the name already exists.</exception>
    Task CreateAgentAsync(AgentSettings settingsAgent, Func<SettingsAgents?, AgentSettings[]> getAgents, Action<SettingsAgents, AgentSettings[]> setAgents, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="originalAgentName">Name of the original agent.</param>
    /// <param name="getAgents">The get agents.</param>
    /// <param name="setAgents">The set agents.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="AlreadyExistsException">Indicates that an agent with the name already exists</exception>
    /// <exception cref="NotFoundException">Agent doesn't exist</exception>
    Task UpdateAgentAsync(AgentSettings settingsAgent, string originalAgentName, Func<SettingsAgents?, AgentSettings[]> getAgents, Action<SettingsAgents, AgentSettings[]> setAgents, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the agent.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="getAgents">The get agents.</param>
    /// <param name="setAgents">The set agents.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException"></exception>
    Task DeleteAgentAsync(string name, Func<SettingsAgents?, AgentSettings[]> getAgents, Action<SettingsAgents, AgentSettings[]> setAgents, CancellationToken cancellationToken);

    /// <summary>
    /// Get settings
    /// </summary>
    /// <returns>Setting</returns>
    Task<Model.Internal.Settings> GetSettingsAsync(CancellationToken cancellationToken);
}
