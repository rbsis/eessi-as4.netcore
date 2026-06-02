using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Exceptions.Handlers;
using Eu.EDelivery.AS4.Model.Internal;
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
            .AddSingleton(new Mock<IDefaultAgentReceiverRegistry>().Object)
            .AddSingleton(new Mock<IDefaultAgentTransformerRegistry>().Object)
            .AddSingleton(new Mock<IDefaultAgentStepRegistry>().Object)
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
        if (type == AgentType.Retry)
        {
            // The Retry agent is a special case, as it is the only agent that does not have a default transformer, but instead uses a custom implementation of the retry logic in the receiver. Therefore, we skip this case in this test.
            return true.ToProperty();
        }

        // Arrange
        var defaultAgentTransformerRegistry = new DefaultAgentTransformerRegistry();
        var expected = defaultAgentTransformerRegistry.GetDefaultTransformer(type);
        var json = JsonConvert.SerializeObject(expected);

        // Act
        var actual = JsonConvert.DeserializeObject<Transformer>(json);

        // Assert
        Assert.NotNull(actual);
        var sameDefault = expected.Type == actual.Type;

        return sameDefault.ToProperty();
    }

    [Property]
    public Property OtherTransformersAreSerializable(AgentType type)
    {
        if (type == AgentType.Retry)
        {
            // The Retry agent is a special case, as it is the only agent that does not have a default transformer, but instead uses a custom implementation of the retry logic in the receiver. Therefore, we skip this case in this test.
            return true.ToProperty();
        }

        // Arrange
        var defaultAgentTransformerRegistry = new DefaultAgentTransformerRegistry();
        var expected = defaultAgentTransformerRegistry.GetOtherTransformers(type);
        var json = JsonConvert.SerializeObject(expected);

        // Act
        var actual = JsonConvert.DeserializeObject<Transformer[]>(json);

        // Assert
        Assert.NotNull(actual);
        var sameOthers = expected
            .Zip(actual, (t1, t2) => t1.Type == t2.Type)
            .All(x => x);

        return sameOthers.ToProperty();
    }

    [Fact]
    public void RegistryContainsDefaultConfigurationForAllAgentTypes()
    {
        var defaultAgentStepRegistry = new DefaultAgentStepRegistry();
        Assert.All(
            Enum.GetValues(typeof(AgentType)).Cast<AgentType>().Where(x => x != AgentType.Retry),
            t => Assert.NotNull(defaultAgentStepRegistry.GetDefaultStepConfiguration(t)));
    }

    [Fact]
    public void RegistryContainsDefaultTransformerForAllAgentTypes()
    {
        var defaultAgentTransformerRegistry = new DefaultAgentTransformerRegistry();
        Assert.All(
            Enum.GetValues(typeof(AgentType)).Cast<AgentType>().Where(x => x != AgentType.Retry),
            t => Assert.NotNull(defaultAgentTransformerRegistry.GetDefaultTransformer(t)));
    }
}
