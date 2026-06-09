using Eu.EDelivery.AS4.Fe.Exceptions;
using Eu.EDelivery.AS4.Fe.Services;
using Eu.EDelivery.AS4.Fe.Settings;
using Eu.EDelivery.AS4.Model.Internal;
using NSubstitute;

namespace Eu.EDelivery.AS4.Fe.UnitTests;

public class As4SettingsServiceTests
{
    private const string SubmitAgentName = "submitAgentName";
    private const string SubmitAgentName2 = "submitAgentName2";
    private const string ReceiveAgentName = "receiveAgentName";
    private readonly Model.Internal.Settings _settingsList;

    private readonly AgentSettings _submitAgent = new()
    {
        Name = SubmitAgentName
    };

    private As4SettingsService _settingsService;
    private ISettingsSource _settingsSource;

    protected As4SettingsServiceTests()
    {
        _settingsList = new Model.Internal.Settings
        {
            Agents = new SettingsAgents
            {
                SubmitAgents =
                [
                    _submitAgent,
                    new AgentSettings
                    {
                        Name = SubmitAgentName2
                    }
                ],
                ReceiveAgents =
                [
                    new AgentSettings
                    {
                        Name = ReceiveAgentName
                    }
                ]
            }
        };
        _settingsSource = Substitute.For<ISettingsSource>();
        _settingsService = new As4SettingsService(_settingsSource);
    }

    private void Setup(CancellationToken cancellationToken)
    {
        _settingsSource = Substitute.For<ISettingsSource>();
        _settingsSource.GetAsync(cancellationToken).Returns(_settingsList);
        _settingsService = new As4SettingsService(_settingsSource);
    }

    public class CreateAgent : As4SettingsServiceTests
    {
        [Fact]
        public async Task Add_Agent_When_Original_List_Is_Empty()
        {
            // Arrange
            var newAgent = new AgentSettings
            {
                Name = "newAgent"
            };

            Setup(TestContext.Current.CancellationToken);

            _settingsSource.GetAsync(TestContext.Current.CancellationToken).Returns(new Model.Internal.Settings
            {
                Agents = new SettingsAgents()
            });

            // Act
            await _settingsService.CreateAgentAsync(newAgent, agents => agents?.ReceiveAgents ?? [], (settings, agt) => settings.ReceiveAgents = agt, TestContext.Current.CancellationToken);

            // Assert
            await _settingsSource.Received().SaveAsync(
                Arg.Is<Model.Internal.Settings>(settings => settings.Agents!.ReceiveAgents!.Any(agent => agent.Name == newAgent.Name)),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Creates_Agent_When_It_Doesnt_Exist_Yet()
        {
            // Arrange
            var newAgentName = "newAgent";
            var newAgent = new AgentSettings
            {
                Name = newAgentName
            };

            Setup(TestContext.Current.CancellationToken);

            // Act & Assert
            await _settingsService.CreateAgentAsync(
                newAgent,
                agents => agents?.SubmitAgents ?? [],
                (settings, agents) => settings.SubmitAgents = agents,
                TestContext.Current.CancellationToken);
            await _settingsSource.Received().SaveAsync(
                Arg.Is<Model.Internal.Settings>(settings => settings.Agents!.SubmitAgents!.Any(agent => agent.Name == newAgentName)),
                Arg.Any<CancellationToken>());

            await _settingsService.CreateAgentAsync(
                newAgent,
                agents => agents?.ReceiveAgents ?? [],
                (settings, agents) => settings.ReceiveAgents = agents,
                TestContext.Current.CancellationToken);
            await _settingsSource.Received().SaveAsync(
                Arg.Is<Model.Internal.Settings>(settings => settings.Agents!.ReceiveAgents!.Any(agent => agent.Name == newAgentName)),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Throws_Exception_When_Agent_With_Name_Already_Exists()
        {
            // Arrange
            var newAgent = new AgentSettings { Name = SubmitAgentName };

            Setup(TestContext.Current.CancellationToken);

            // Act & Assert
            await Assert.ThrowsAsync<AlreadyExistsException>(() => _settingsService.CreateAgentAsync(
                newAgent,
                agents => agents?.SubmitAgents ?? [],
                (settings, agents) => settings.SubmitAgents = agents,
                TestContext.Current.CancellationToken));
        }
    }

    public class UpdateAgent : As4SettingsServiceTests
    {
        [Fact]
        public async Task Throws_Exception_When_Agent_Not_Found()
        {
            // Arrange
            Setup(TestContext.Current.CancellationToken);

            var newAgent = new AgentSettings { Name = "NEW RANDOM NAME" };

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _settingsService.UpdateAgentAsync(
                newAgent, "fdsqfd",
                settings => settings!.SubmitAgents!,
                (settings, agents) => settings.SubmitAgents = agents,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Throws_Exception_When_Agent_With_Name_Already_Exists()
        {
            // Arrange
            Setup(TestContext.Current.CancellationToken);

            // Act
            await Assert.ThrowsAsync<AlreadyExistsException>(() => _settingsService.UpdateAgentAsync(
                new AgentSettings { Name = SubmitAgentName },
                SubmitAgentName2,
                settings => settings!.SubmitAgents!,
                (settings, agents) => settings.SubmitAgents = agents,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Updates()
        {
            // Arrange
            Setup(TestContext.Current.CancellationToken);

            // Act
            await _settingsService.UpdateAgentAsync(
                new AgentSettings { Name = "NEW" },
                _submitAgent.Name,
                settings => settings!.SubmitAgents!,
                (settings, agents) => settings.SubmitAgents = agents,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(_settingsList.Agents!.SubmitAgents!, agent => agent.Name == "NEW");
            await _settingsSource.Received().SaveAsync(
                Arg.Is<Model.Internal.Settings>(x => x.Agents!.SubmitAgents!.Any(agt => agt.Name == "NEW")),
                Arg.Any<CancellationToken>());
        }
    }

    public class DeleteAgent : As4SettingsServiceTests
    {
        [Fact]
        public async Task Deletes_Agent()
        {
            // Arrange
            Setup(TestContext.Current.CancellationToken);

            // Act & Assert
            await _settingsService.DeleteAgentAsync(
                SubmitAgentName,
                agents => agents!.SubmitAgents!,
                (settings, agents) => settings.SubmitAgents = agents,
                TestContext.Current.CancellationToken);
            await _settingsSource.Received().SaveAsync(
                Arg.Is<Model.Internal.Settings>(x => x.Agents!.SubmitAgents!.All(agt => agt.Name != SubmitAgentName)),
                Arg.Any<CancellationToken>());
            await _settingsService.DeleteAgentAsync(
                ReceiveAgentName,
                agents => agents!.ReceiveAgents!,
                (settings, agents) => settings.ReceiveAgents = agents,
                TestContext.Current.CancellationToken);
            await _settingsSource.Received().SaveAsync(
                Arg.Is<Model.Internal.Settings>(x => x.Agents!.ReceiveAgents!.All(agt => agt.Name != ReceiveAgentName)),
                Arg.Any<CancellationToken>());

            await _settingsSource.Received().GetAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Throws_Exception_when_Agent_Not_Exists()
        {
            // Arrange
            Setup(TestContext.Current.CancellationToken);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _settingsService.DeleteAgentAsync(
                "IDONTEXISTAGENT",
                agents => agents!.SubmitAgents!,
                (settings, agents) => settings.SubmitAgents = agents,
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<NotFoundException>(() => _settingsService.DeleteAgentAsync(
                "IDONTEXISTAGENT",
                agents => agents!.ReceiveAgents!,
                (settings, agents) => settings.ReceiveAgents = agents,
                TestContext.Current.CancellationToken));

            await _settingsSource.DidNotReceive().SaveAsync(
                Arg.Any<Model.Internal.Settings>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Throws_Exception_When_Parameters_Are_Empty()
        {
            // Arrange
            Setup(TestContext.Current.CancellationToken);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _settingsService.DeleteAgentAsync("", null!, null!, TestContext.Current.CancellationToken));
        }
    }

    public class SavePullSend : As4SettingsServiceTests
    {
        [Fact]
        public async Task Saves_Pull_Send_Settings()
        {
            // Arrange
            Setup(TestContext.Current.CancellationToken);
            _settingsSource.GetAsync(TestContext.Current.CancellationToken).Returns(new Model.Internal.Settings { PullSend = null });

            var fixture = new SettingsPullSend { AuthorizationMapPath = "./my-security-path/pull_authorization_map.xml" };

            // Act
            await _settingsSource.SaveAsync(new Model.Internal.Settings { PullSend = fixture }, TestContext.Current.CancellationToken);

            // Assert
            var expected = Arg.Is<Model.Internal.Settings>(s => s.PullSend!.AuthorizationMapPath == fixture.AuthorizationMapPath);
            await _settingsSource.Received().SaveAsync(expected, Arg.Any<CancellationToken>());
        }
    }

    public class Submit : As4SettingsServiceTests
    {
        [Fact]
        public async Task Saves_Submit_Settings()
        {
            // Arrange
            Setup(TestContext.Current.CancellationToken);
            _settingsSource.GetAsync(TestContext.Current.CancellationToken).Returns(new Model.Internal.Settings { Submit = null });

            var fixture = new SettingsSubmit { PayloadRetrievalPath = "./my-attachment-path/" };

            // Act
            await _settingsSource.SaveAsync(new Model.Internal.Settings { Submit = fixture }, TestContext.Current.CancellationToken);

            // Assert
            var expected = Arg.Is<Model.Internal.Settings>(s => s.Submit!.PayloadRetrievalPath == fixture.PayloadRetrievalPath);
            await _settingsSource.Received().SaveAsync(expected, Arg.Any<CancellationToken>());
        }
    }
}
