using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Exceptions.Handlers;
using Eu.EDelivery.AS4.Receivers;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.ServiceHandler.Providers;
using Eu.EDelivery.AS4.Services;
using Eu.EDelivery.AS4.Services.Journal;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Transformers;
using Eu.EDelivery.AS4.UnitTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;

namespace Eu.EDelivery.AS4.UnitTests.Agents;

public class GivenAgentProviderFacts
{

    [Fact]
    public void ThrowsExceptionWhenBuildingAgents()
    {
        // Arrange
        var stubRegistry = new Mock<IExceptionHandlerRegistry>();
        stubRegistry.Setup(x => x.GetHandler(It.IsAny<AgentType>()))
            .Returns(Default.LogExceptionHandler);

        var stubReceiverBuilder = new Mock<IReceiverBuilder>();
        var stubTransformerBuilder = new Mock<ITransformerBuilder>();

        var expected = new Exception("ignored string");

        // Act / Assert
        var actual = Assert.Throws<Exception>(() => new AgentProvider(
            NullLogger<AgentProvider>.Instance,
            new SaboteurAgentConfig(expected),
            stubRegistry.Object,
            stubReceiverBuilder.Object,
            stubTransformerBuilder.Object,
            new ServiceCollection().BuildServiceProvider()));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AssembleAgentBaseClassesIfTypeIsSpecified()
    {
        // Arrange
        // Minder agents are being created and uses the registry
        var stubRegistry = new Mock<IExceptionHandlerRegistry>();
        stubRegistry.Setup(x => x.GetHandler(It.IsAny<AgentType>()))
            .Returns(Default.LogExceptionHandler);

        var stubReceiverBuilder = new Mock<IReceiverBuilder>();
        var stubTransformerBuilder = new Mock<ITransformerBuilder>();

        var serviceProvider = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(new Mock<IDbContextFactory<DatastoreContext>>().Object)
            .AddSingleton(new Mock<IDatastoreRepository>().Object)
            .AddSingleton(new Mock<IInMessageService>().Object)
            .AddSingleton(new Mock<IAS4MessageBodyStore>().Object)
            .AddSingleton(new Mock<IStepBuilder>().Object)
            .AddKeyedSingleton<IJournalLogger>(typeof(JournalDatastoreLogger), new Mock<IJournalLogger>().Object)
            .AddAS4Receivers()
            .BuildServiceProvider();

        // Act
        var sut = new AgentProvider(
            NullLogger<AgentProvider>.Instance,
            new SingleAgentConfig(),
            stubRegistry.Object,
            stubReceiverBuilder.Object,
            stubTransformerBuilder.Object,
            serviceProvider);

        // Assert
        Assert.NotEmpty(sut.Agents);
    }

    [Property]
    public Property DefaultTransformersAreSerializable(AgentType type)
    {
        // Arrange
        var expected = AgentProvider.GetDefaultTransformerForAgentType(type);
        var json = JsonConvert.SerializeObject(expected);

        // Act
        var actual = JsonConvert.DeserializeObject<TransformerConfigEntry>(json);

        // Assert
        Assert.NotNull(actual);
        var sameDefault = expected.DefaultTransformer.Type == actual.DefaultTransformer.Type;
        var sameOthers = expected.OtherTransformers
            .Zip(actual.OtherTransformers, (t1, t2) => t1.Type == t2.Type)
            .All(x => x);

        return sameDefault.ToProperty().And(sameOthers);
    }

    [Fact]
    public void RegistryContainsDefaultConfigurationForAllAgentTypes()
    {
        Assert.All(
            Enum.GetValues(typeof(AgentType)).Cast<AgentType>(),
            t => Assert.NotNull(AgentProvider.GetDefaultStepConfigurationForAgentType(t)));
    }

    [Fact]
    public void RegistryContainsDefaultTransformerForAllAgentTypes()
    {
        Assert.All(
            Enum.GetValues(typeof(AgentType)).Cast<AgentType>(),
            t => Assert.NotNull(AgentProvider.GetDefaultTransformerForAgentType(t)));
    }
}
