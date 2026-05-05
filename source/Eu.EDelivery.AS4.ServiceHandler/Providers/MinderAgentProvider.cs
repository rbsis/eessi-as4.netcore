using System.Diagnostics.CodeAnalysis;
using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Exceptions.Handlers;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Receivers.Http;
using Eu.EDelivery.AS4.Services.Journal;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Receive;
using Eu.EDelivery.AS4.Steps.Send;
using Eu.EDelivery.AS4.Steps.Submit;
using Eu.EDelivery.AS4.Transformers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.ServiceHandler.Providers;

[ExcludeFromCodeCoverage]
internal class MinderAgentProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITransformerBuilder _transformerBuilder;

    public MinderAgentProvider(IServiceProvider serviceProvider, ITransformerBuilder transformerBuilder)
    {
        _serviceProvider = serviceProvider;
        _transformerBuilder = transformerBuilder;
    }

    internal IEnumerable<IAgent> GetMinderSpecificAgentsFromConfig(IConfig config)
    {
        var minderTestAgents = config.GetEnabledMinderTestAgents()
            ?? throw new InvalidOperationException(@"MinderAgentProvider requires a collection of AgentConfig instances from the IConfig.GetEnabledMinderTestAgents() call");

        foreach (var agent in minderTestAgents)
        {
            yield return CreateMinderTestAgent(agent.Url, agent.UseLogging, agent.Transformer);
        }
    }

    [ExcludeFromCodeCoverage]
    private Agent CreateMinderTestAgent(string? url, bool useLogging, Transformer? transformerConfig)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        ArgumentNullException.ThrowIfNull(transformerConfig);

        var receiver = _serviceProvider.GetRequiredService<HttpReceiver>();
        receiver.Configure([new Setting("Url", url), new Setting("UseLogging", useLogging.ToString())]);

        var stepBuilder = _serviceProvider.GetRequiredService<IStepBuilder>();
        var exceptionHandler = _serviceProvider.GetRequiredService<MinderExceptionHandler>();
        var stepExecutioner = new StepExecutioner(
            stepBuilder.BuildSteps(CreateMinderHappyFlow()),
            stepBuilder.BuildSteps(CreateReceiveUnhappyFlow()),
            exceptionHandler);

        return new Agent(
            logger: _serviceProvider.GetRequiredService<ILogger<Agent>>(),
            config: new AgentConfig(name: "Minder Submit/Receive Agent"),
            receiver: receiver,
            transformer: _transformerBuilder.BuildFromConfig(transformerConfig),
            exceptionHandler: exceptionHandler,
            steps: stepExecutioner,
            journalLogger: NoopJournalLogger.Instance);

    }

    [ExcludeFromCodeCoverage]
    private static ConditionalStepConfig CreateMinderHappyFlow()
    {
        static bool IsSubmitMessage(MessagingContext m) => m.Mode == MessagingContextMode.Submit;

        var submitStepConfig = CreateSubmitSteps();
        var receiveStepConfig = CreateReceiveHappyFlowSteps();

        return new ConditionalStepConfig(IsSubmitMessage, submitStepConfig, receiveStepConfig);
    }

    [ExcludeFromCodeCoverage]
    private static ConditionalStepConfig CreateReceiveUnhappyFlow()
    {
        static bool IsSubmitMessage(MessagingContext m) => m.Mode == MessagingContextMode.Submit;

        var submitStepConfig = Array.Empty<Step>();
        var receiveStepConfig = CreateReceiveUnhappyFlowSteps();

        return new ConditionalStepConfig(IsSubmitMessage, submitStepConfig, receiveStepConfig);
    }

    [ExcludeFromCodeCoverage]
    private static Step[] CreateSubmitSteps() =>
    [
        new() {Type = typeof(StoreAS4MessageStep).AssemblyQualifiedName!},
        new() {Type = typeof(CreateAS4ReceiptStep).AssemblyQualifiedName!}
    ];

    [ExcludeFromCodeCoverage]
    private static Step[] CreateReceiveHappyFlowSteps() =>
    [
        new() {Type = typeof(SaveReceivedMessageStep).AssemblyQualifiedName!},
        new() {Type = typeof(DeterminePModesStep).AssemblyQualifiedName!},
        new() {Type = typeof(ValidateAS4MessageStep).AssemblyQualifiedName!},
        new() {Type = typeof(DecryptAS4MessageStep).AssemblyQualifiedName!},
        new() {Type = typeof(VerifySignatureAS4MessageStep).AssemblyQualifiedName!},
        new() {Type = typeof(DecompressAttachmentsStep).AssemblyQualifiedName!},
        new() {Type = typeof(UpdateReceivedAS4MessageBodyStep).AssemblyQualifiedName!},
        new() {Type = typeof(CreateAS4ReceiptStep).AssemblyQualifiedName!},
        new() {Type = typeof(SignAS4MessageStep).AssemblyQualifiedName!},
        new() {Type = typeof(CreateAS4SignalMessageStep).AssemblyQualifiedName!},
    ];

    [ExcludeFromCodeCoverage]
    private static Step[] CreateReceiveUnhappyFlowSteps() =>
    [
        new() {Type = typeof(CreateAS4ErrorStep).AssemblyQualifiedName!},
        new() {Type = typeof(SignAS4MessageStep).AssemblyQualifiedName!},
        new() {Type = typeof(CreateAS4SignalMessageStep).AssemblyQualifiedName!}
    ];
}
