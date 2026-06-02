using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Exceptions.Handlers;
using Eu.EDelivery.AS4.Receivers;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Services;
using Eu.EDelivery.AS4.Services.Journal;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Transformers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.ServiceHandler.Providers;

/// <summary>
/// Agent Provider/Manager Resposibility:
/// manage the registered Agents (default and extendible)
/// </summary>
public class AgentProvider
{
    private readonly ILogger<AgentProvider> _logger;
    private readonly IExceptionHandlerRegistry _exceptionHandlerRegistry;
    private readonly IReceiverBuilder _receiverBuilder;
    private readonly ITransformerBuilder _transformerBuilder;
    private readonly IServiceProvider _serviceProvider;
    private readonly MinderAgentProvider _minderAgentProvider;

    /// <summary>
    /// Return all the Registered <see cref="IAgent" /> implementations
    /// </summary>
    public IEnumerable<IAgent> Agents { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentProvider"/> class. Create a <see cref="AgentProvider"/> with the Core and Custom Agents
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="config"></param>
    /// <param name="exceptionHandlerRegistry"></param>
    /// <param name="receiverBuilder"></param>
    /// <param name="transformerBuilder"></param>
    /// <param name="serviceProvider"></param>
    public AgentProvider(
        ILogger<AgentProvider> logger,
        IConfig config,
        IExceptionHandlerRegistry exceptionHandlerRegistry,
        IReceiverBuilder receiverBuilder,
        ITransformerBuilder transformerBuilder,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _exceptionHandlerRegistry = exceptionHandlerRegistry;
        _receiverBuilder = receiverBuilder;
        _transformerBuilder = transformerBuilder;
        _serviceProvider = serviceProvider;

        _minderAgentProvider = new MinderAgentProvider(_serviceProvider, _transformerBuilder);

        Agents = BuildFromConfig(config);
    }

    /// <summary>
    /// Creates an <see cref="AgentProvider"/> based on the configured <paramref name="config"/>.
    /// </summary>
    /// <param name="config">Configuration to build the <see cref="IAgent"/> implementations.</param>
    /// <returns></returns>
    private IEnumerable<IAgent> BuildFromConfig(IConfig config)
    {
        if (!config.IsInitialized)
        {
            throw new InvalidOperationException("AgentProvider requires an initialized IConfig implementation to provide Agents");
        }

        var agentConfigs = config.GetAgentsConfiguration();
        if (agentConfigs.Any(c => c is null))
        {
            throw new InvalidOperationException(@"Fails to create IAgent implementations: one or more AgentConfig instances of the IConfig.GetAgentsConfiguration() call is invalid");
        }

        try
        {
            return agentConfigs
                .Select(CreateAgentBaseFromSettings)
                .Concat(_minderAgentProvider.GetMinderSpecificAgentsFromConfig(config))
                .Append(new CleanUpAgent(
                    logger: _serviceProvider.GetRequiredService<ILogger<CleanUpAgent>>(),
                    contextFactory: _serviceProvider.GetRequiredService<IDbContextFactory<DatastoreContext>>(),
                    config))
                .Append(new RetryAgent(
                    logger: _serviceProvider.GetRequiredService<ILogger<RetryAgent>>(),
                    receiver: _serviceProvider.GetRequiredService<DatastoreReceiver>(),
                    repository: _serviceProvider.GetRequiredService<IDatastoreRepository>(),
                    inMessageService: _serviceProvider.GetRequiredService<IInMessageService>())
                )
                .ToArray();
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "BuildFromConfig failed");
            throw;
        }
    }

    private IAgent CreateAgentBaseFromSettings(AgentConfig config)
    {
        ArgumentNullException.ThrowIfNull(config.Settings);

        var agentLogTag = $"{config.Type} Agent {config.Name}";

        if (config.Settings.StepConfiguration?.NormalPipeline != null
            && config.Settings.StepConfiguration.NormalPipeline.Any(s => s?.Type == null))
        {
            throw new InvalidOperationException($@"{agentLogTag} has one ore more Steps in the NormalPipeline without a Type");
        }

        if (config.Settings.StepConfiguration?.ErrorPipeline != null
            && config.Settings.StepConfiguration.ErrorPipeline.Any(s => s?.Type == null))
        {
            throw new ArgumentNullException($@"{agentLogTag} has one or more Steps in the ErrorPipeline without a Type");
        }

        var defaultAgentStepRegistry = _serviceProvider.GetRequiredService<IDefaultAgentStepRegistry>();
        var stepConfiguration = config.Settings.StepConfiguration ?? defaultAgentStepRegistry.GetDefaultStepConfiguration(config.Type);
        var stepBuilder = _serviceProvider.GetRequiredService<IStepBuilder>();
        var exceptionHandler = _exceptionHandlerRegistry.GetHandler(config.Type);
        var stepExecutioner = new StepExecutioner(
            stepBuilder.BuildSteps(stepConfiguration.NormalPipeline ?? []),
            stepBuilder.BuildSteps(stepConfiguration.ErrorPipeline ?? []),
            exceptionHandler);

        var defaultAgentReceiverRegistry = _serviceProvider.GetRequiredService<IDefaultAgentReceiverRegistry>();
        var defaultAgentTransformerRegistry = _serviceProvider.GetRequiredService<IDefaultAgentTransformerRegistry>();

        return new Agent(
            logger: _serviceProvider.GetRequiredService<ILogger<Agent>>(),
            config: config,
            receiver: _receiverBuilder.BuildFromConfig(config.Settings.Receiver
                ?? defaultAgentReceiverRegistry.GetDefaultReceiver(config.Type)),
            transformer: _transformerBuilder.BuildFromConfig(config.Settings.Transformer
                ?? defaultAgentTransformerRegistry.GetDefaultTransformer(config.Type)),
            exceptionHandler: exceptionHandler,
            steps: stepExecutioner,
            journalLogger: _serviceProvider.GetRequiredKeyedService<IJournalLogger>(typeof(JournalDatastoreLogger)));
    }
}
