using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Receivers;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.UnitTests.Exceptions.Handlers;
using Eu.EDelivery.AS4.UnitTests.Model;
using Eu.EDelivery.AS4.UnitTests.Receivers;
using Eu.EDelivery.AS4.UnitTests.Steps;
using Eu.EDelivery.AS4.UnitTests.Transformers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Eu.EDelivery.AS4.UnitTests.Agents;

public class GivenAgentFacts
{
    [Fact]
    public async Task StopReceiverIfAgentsStopped()
    {
        // Arrange
        var spyReceiver = Mock.Of<IReceiver>();
        var exceptionHandler = new SpyAgentExceptionHandler();
        var stepExecutioner = new StepExecutioner([], [], exceptionHandler);

        var sut = new Agent(
            NullLogger<Agent>.Instance,
            new AgentConfig(name: "Agent with Spy Receiver"),
            spyReceiver,
            new StubSubmitTransformer(),
            exceptionHandler,
            steps: stepExecutioner);

        // Act
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Mock.Get(spyReceiver).Verify(r => r.StopReceiving());
    }

    [Fact]
    public async Task NoStepsAreExecutedIfNoStepsAreDefined()
    {
        // Arrange
        var spyReceiver = new SpyReceiver();
        var exceptionHandler = new SpyAgentExceptionHandler();
        var stepExecutioner = new StepExecutioner([], [], exceptionHandler);

        var sut = new Agent(
            NullLogger<Agent>.Instance,
            new AgentConfig("Agent with non-defined Normal Steps"),
            spyReceiver,
            new StubSubmitTransformer(),
            exceptionHandler,
            stepExecutioner);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        Assert.True(spyReceiver.IsCalled);
        Assert.NotNull(spyReceiver.Context);
        Assert.NotNull(spyReceiver.Context.SubmitMessage);
    }

    [Fact]
    public async Task ReceiverGetsExpectedContextIfHappyPath()
    {
        // Arrange
        var spyReceiver = new SpyReceiver();
        var exceptionHandler = new SpyAgentExceptionHandler();
        var provider = new ServiceCollection()
            .AddSingleton<StubAS4MessageStep>()
            .BuildServiceProvider();
        var stepBuilder = new StepBuilder(NullLogger<StepBuilder>.Instance, provider);
        var stepExecutioner = new StepExecutioner(stepBuilder.BuildSteps(AS4MessageSteps()), [], exceptionHandler);

        var sut = new Agent(
            NullLogger<Agent>.Instance,
            new AgentConfig(name: "Agent with Normal Pipeline"),
            spyReceiver,
            new StubSubmitTransformer(),
            exceptionHandler,
            stepExecutioner);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        Assert.True(spyReceiver.IsCalled);
        Assert.NotNull(spyReceiver.Context);
        Assert.Equal(AS4Message.Empty, spyReceiver.Context.AS4Message);
    }

    private static Step[] AS4MessageSteps()
    {
        return [new Step { Type = typeof(StubAS4MessageStep).AssemblyQualifiedName! }];
    }

    [Fact]
    public async Task HandlesTransformFailure()
    {
        // Arrange
        var spyHandler = new SpyAgentExceptionHandler();
        var spyReceiver = new SpyReceiver();
        var sut = AgentWithSaboteurTransformer(spyHandler, spyReceiver);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        Assert.True(spyReceiver.IsCalled);
        Assert.True(
            spyHandler.HandledTransformationException,
            "Spy Agent exception handler should have handled transformation exception");
    }

    private static Agent AgentWithSaboteurTransformer(IAgentExceptionHandler spyHandler, IReceiver spyReceiver)
    {
        var stepExecutioner = new StepExecutioner([], [], spyHandler);

        return new Agent(
            NullLogger<Agent>.Instance,
            new AgentConfig(name: "Agent with Saboteur Transformer"),
            spyReceiver,
            new SaboteurTransformer(),
            spyHandler,
            stepExecutioner);
    }

    [Fact]
    public async Task HandlesFailureInHappyPath()
    {
        // Arrange
        var spyHandler = new SpyAgentExceptionHandler();
        var spyReceiver = new SpyReceiver();
        var sut = AgentWithHappySaboteurSteps(spyHandler, spyReceiver);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        Assert.True(spyReceiver.IsCalled);
        Assert.True(
            spyHandler.HandledExecutionException,
            "Spy Agent exception handler should have handled execution exception");
    }

    private static Agent AgentWithHappySaboteurSteps(IAgentExceptionHandler spyHandler, IReceiver spyReceiver)
    {
        var stepExecutioner = new StepExecutioner([new SaboteurStep()], [], spyHandler);

        return new Agent(
            NullLogger<Agent>.Instance,
            new AgentConfig(name: "Agent with Saboteur Steps in Normal Pipeline"),
            spyReceiver,
            new StubSubmitTransformer(),
            spyHandler,
            stepExecutioner);
    }

    [Fact]
    public async Task HandlesFailureInUnhappyPath()
    {
        // Arrange
        var spyHandler = new SpyAgentExceptionHandler();
        var spyReceiver = new SpyReceiver();
        var sut = AgentWithUnhappySaboteurSteps(spyHandler, spyReceiver);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        Assert.True(spyReceiver.IsCalled);
        Assert.True(
            spyHandler.HandledErrorException,
            "Spy Agent exception handler should have handled error exception");
    }

    private static Agent AgentWithUnhappySaboteurSteps(IAgentExceptionHandler spyHandler, IReceiver spyReceiver)
    {
        var stepExecutioner = new StepExecutioner([new UnsuccessfulStep()], [new SaboteurStep()], spyHandler);

        return new Agent(
            NullLogger<Agent>.Instance,
            new AgentConfig(name: "Agent with Saboteur Steps in Error Pipeline"),
            spyReceiver,
            new StubSubmitTransformer(),
            spyHandler,
            stepExecutioner);
    }

    [Fact]
    public async Task RunsThroughUnhappyPathIfAnyHappyStepIndicatesUnsuccesful()
    {
        // Arrange
        var spyReceiver = new SpyReceiver();
        var sut = AgentWithUnsuccesfulStep(spyReceiver);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        Assert.True(spyReceiver.IsCalled);
        Assert.IsType<UnHappyContext>(spyReceiver.Context);
    }

    private static Agent AgentWithUnsuccesfulStep(IReceiver spyReceiver)
    {
        var exceptionHandler = new SpyAgentExceptionHandler();
        var stepExecutioner = new StepExecutioner([new UnsuccessfulStep()], [new UnhappyStep()], exceptionHandler);

        return new Agent(
            NullLogger<Agent>.Instance,
            new AgentConfig(name: "Agent with Steps that don't succeed succesfully"),
            spyReceiver,
            new StubSubmitTransformer(),
            exceptionHandler,
            stepExecutioner);
    }

    public class UnsuccessfulStep : IStep
    {
        /// <summary>
        /// Execute the step for a given <paramref name="messagingContext"/>.
        /// </summary>
        /// <param name="messagingContext">Message used during the step execution.</param>
        /// <returns></returns>
        /// <param name="cancellation"></param>
        public Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
        {
            var result = new ErrorResult(description: "error", alias: default);

            return Task.FromResult(StepResult.Failed(new HappyContext { ErrorResult = result }));
        }
    }

    public class UnhappyStep : IStep
    {
        /// <summary>
        /// Execute the step for a given <paramref name="messagingContext"/>.
        /// </summary>
        /// <param name="messagingContext">Message used during the step execution.</param>
        /// <returns></returns>
        /// <param name="cancellation"></param>
        public Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
        {
            return Task.FromResult(StepResult.Success(new UnHappyContext()));
        }
    }

    public class HappyContext : EmptyMessagingContext
    { }

    public class UnHappyContext : EmptyMessagingContext
    { }
}
