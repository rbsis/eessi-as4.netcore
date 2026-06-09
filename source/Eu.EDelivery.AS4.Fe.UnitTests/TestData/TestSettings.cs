using Eu.EDelivery.AS4.Fe.Models;
using Eu.EDelivery.AS4.Fe.Services;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Fe.UnitTests.TestData;

public class TestSettings : IAs4SettingsService
{
    public Task CreateAgentAsync(AgentSettings settingsAgent, Func<SettingsAgents?, AgentSettings[]> getAgents, Action<SettingsAgents, AgentSettings[]> setAgents, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAgentAsync(string name, Func<SettingsAgents?, AgentSettings[]> getAgents, Action<SettingsAgents, AgentSettings[]> setAgents, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<Model.Internal.Settings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SaveBaseSettingsAsync(BaseSettings settings, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SaveCustomSettingsAsync(CustomSettings settings, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SaveDatabaseSettingsAsync(SettingsDatabase settings, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SavePullSendSettingsAsync(SettingsPullSend settings, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SaveSubmitSettingsAsync(SettingsSubmit settings, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task UpdateAgentAsync(AgentSettings settingsAgent, string originalAgentName, Func<SettingsAgents?, AgentSettings[]> getAgents, Action<SettingsAgents, AgentSettings[]> setAgents, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
