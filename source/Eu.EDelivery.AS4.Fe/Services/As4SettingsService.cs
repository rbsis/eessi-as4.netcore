using EnsureThat;
using Eu.EDelivery.AS4.Fe.Exceptions;
using Eu.EDelivery.AS4.Fe.Models;
using Eu.EDelivery.AS4.Fe.Settings;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Fe.Services;

/// <summary>
/// Service to manage settings.xml
/// </summary>
/// <seealso cref="IAs4SettingsService" />
public class As4SettingsService : IAs4SettingsService
{
    private readonly ISettingsSource _settingsSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="As4SettingsService"/> class.
    /// </summary>
    /// <param name="settingsSource">The settings source.</param>
    public As4SettingsService(ISettingsSource settingsSource)
    {
        _settingsSource = settingsSource;
    }

    /// <summary>
    /// Saves the base settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task SaveBaseSettingsAsync(BaseSettings settings, CancellationToken cancellationToken)
    {
        var file = await GetSettingsAsync(cancellationToken);
        file.IdFormat = settings.IdFormat;
        file.RetentionPeriod = settings.RetentionPeriod.ToString();
        file.RetryReliability = settings.RetryReliability;
        file.CertificateStore = settings.CertificateStore;

        await _settingsSource.SaveAsync(file, cancellationToken);
    }

    /// <summary>
    /// Saves the submit settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task SaveSubmitSettingsAsync(SettingsSubmit settings, CancellationToken cancellationToken)
    {
        var file = await GetSettingsAsync(cancellationToken);
        file.Submit = settings;

        await _settingsSource.SaveAsync(file, cancellationToken);
    }

    /// <summary>
    /// Saves the pull send settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task SavePullSendSettingsAsync(SettingsPullSend settings, CancellationToken cancellationToken)
    {
        var file = await GetSettingsAsync(cancellationToken);
        file.PullSend = settings;

        await _settingsSource.SaveAsync(file, cancellationToken);
    }

    /// <summary>
    /// Saves the custom settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task SaveCustomSettingsAsync(CustomSettings settings, CancellationToken cancellationToken)
    {
        var file = await GetSettingsAsync(cancellationToken);
        file.CustomSettings = settings;

        await _settingsSource.SaveAsync(file, cancellationToken);
    }

    /// <summary>
    /// Saves the database settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task SaveDatabaseSettingsAsync(SettingsDatabase settings, CancellationToken cancellationToken)
    {
        var file = await GetSettingsAsync(cancellationToken);
        file.Database = settings;

        await _settingsSource.SaveAsync(file, cancellationToken);
    }

    /// <summary>
    /// Creates the agent.
    /// </summary>
    /// <param name="settingsAgent">The settings agent.</param>
    /// <param name="getAgents">The get agents.</param>
    /// <param name="setAgents">The set agents.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="AlreadyExistsException">Indicates that an agent with the name already exists.</exception>
    public async Task CreateAgentAsync(AgentSettings settingsAgent, Func<SettingsAgents?, AgentSettings[]> getAgents, Action<SettingsAgents, AgentSettings[]> setAgents, CancellationToken cancellationToken)
    {
        var file = await GetSettingsAsync(cancellationToken);
        var agents = GetAgents(getAgents, file);
        var existing = agents.FirstOrDefault(agent => StringComparer.OrdinalIgnoreCase.Equals(agent.Name, settingsAgent.Name));
        if (existing != null)
        {
            throw new AlreadyExistsException($"Agent with name {settingsAgent.Name} already exists");
        }

        agents.Add(settingsAgent);

        if (file.Agents != null)
        {
            setAgents(file.Agents, [.. agents]);
        }

        await _settingsSource.SaveAsync(file, cancellationToken);
    }

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
    public async Task UpdateAgentAsync(AgentSettings settingsAgent, string originalAgentName, Func<SettingsAgents?, AgentSettings[]> getAgents, Action<SettingsAgents, AgentSettings[]> setAgents, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNullOrEmpty(originalAgentName, nameof(originalAgentName));

        var file = await GetSettingsAsync(cancellationToken);
        var agents = getAgents(file.Agents);
        // If a rename of an agent is requested then validate that no other agent with the new name exists yet
        if (agents.Any(agt => agt.Name.Equals(settingsAgent.Name, StringComparison.CurrentCultureIgnoreCase) && !agt.Name.Equals(originalAgentName, StringComparison.CurrentCultureIgnoreCase)))
        {
            throw new AlreadyExistsException($"An agent with name {settingsAgent.Name} already exists");
        }

        var agent = agents.FirstOrDefault(agt => agt.Name.Equals(originalAgentName, StringComparison.CurrentCultureIgnoreCase))
            ?? throw new NotFoundException($"{originalAgentName} agent doesn't exist");

        agent.Name = settingsAgent.Name;
        agent.Receiver = settingsAgent.Receiver;
        agent.Transformer = settingsAgent.Transformer;
        agent.StepConfiguration = settingsAgent.StepConfiguration;

        await _settingsSource.SaveAsync(file, cancellationToken);
    }

    /// <summary>
    /// Deletes the agent.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="getAgents">The get agents.</param>
    /// <param name="setAgents">The set agents.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException"></exception>
    public async Task DeleteAgentAsync(string name, Func<SettingsAgents?, AgentSettings[]> getAgents, Action<SettingsAgents, AgentSettings[]> setAgents, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNullOrEmpty(name, nameof(name));

        var file = await GetSettingsAsync(cancellationToken);
        var agents = getAgents(file.Agents);

        var agent = agents.FirstOrDefault(agt => agt.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
            ?? throw new NotFoundException($"Submit agent {name} could not be found");

        var newList = agents.ToList();
        newList.Remove(agent);
        if (file.Agents != null)
        {
            setAgents(file.Agents, [.. newList]);
        }

        await _settingsSource.SaveAsync(file, cancellationToken);
    }

    /// <summary>
    /// Get settings
    /// </summary>
    /// <returns>Setting</returns>
    public async Task<Model.Internal.Settings> GetSettingsAsync(CancellationToken cancellationToken) =>
        await _settingsSource.GetAsync(cancellationToken) ?? throw new NotFoundException("Settings not found");

    private static List<AgentSettings> GetAgents(Func<SettingsAgents?, AgentSettings[]> getAgents, Model.Internal.Settings settings)
    {
        var get = getAgents(settings.Agents);
        return get?.ToList() ?? Enumerable.Empty<AgentSettings>().ToList();
    }
}
