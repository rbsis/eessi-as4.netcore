using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Receivers;
using Eu.EDelivery.AS4.Services.Journal;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Transformers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Agents;

internal class Agent : BackgroundService, IAgent
{
    private readonly ILogger<Agent> _logger;
    private readonly IReceiver _receiver;
    private readonly ITransformer _transformer;
    private readonly IAgentExceptionHandler _exceptionHandler;
    private readonly IStepExecutioner _steps;
    private readonly IJournalLogger _journalLogger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Agent"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="config">The config to add metadata to the agent.</param>
    /// <param name="receiver">The receiver on which the agent should listen for messages.</param>
    /// <param name="transformer">The <see cref="ITransformer"/> instances.</param>
    /// <param name="exceptionHandler">The handler to handle failures during the agent execution.</param>
    /// <param name="steps">The <see cref="IStepExecutioner"/> instance.</param>
    internal Agent(
        ILogger<Agent> logger,
        AgentConfig config,
        IReceiver receiver,
        ITransformer transformer,
        IAgentExceptionHandler exceptionHandler,
        IStepExecutioner steps) :
            this(
                logger,
                config,
                receiver,
                transformer,
                exceptionHandler,
                steps,
                NoopJournalLogger.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Agent"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="config">The config to add metadata to the agent.</param>
    /// <param name="receiver">The receiver on which the agent should listen for messages.</param>
    /// <param name="transformer">The <see cref="ITransformer"/> instances.</param>
    /// <param name="exceptionHandler">The handler to handle failures during the agent execution.</param>
    /// <param name="steps">The <see cref="IStepExecutioner"/> instance.</param>
    /// <param name="journalLogger">The logging implementation to write journal log entries for handled messages.</param>
    internal Agent(
        ILogger<Agent> logger,
        AgentConfig config,
        IReceiver receiver,
        ITransformer transformer,
        IAgentExceptionHandler exceptionHandler,
        IStepExecutioner steps,
        IJournalLogger journalLogger)
    {
        _logger = logger;
        _receiver = receiver;
        _transformer = transformer;
        _exceptionHandler = exceptionHandler;
        _steps = steps;
        _journalLogger = journalLogger;

        AgentConfig = config;
    }

    public AgentConfig AgentConfig { get; }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Factory.StartNew(
        () => _receiver.StartReceiving(OnReceived, stoppingToken), TaskCreationOptions.LongRunning);

    /// <summary>
    /// Starts the specified agent.
    /// </summary>
    /// <param name="cancellationToken">The cancellation.</param>
    /// <returns></returns>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting AS4 Agent {Name}...", AgentConfig.Name);

        await base.StartAsync(cancellationToken);
        //_receiver.StartReceiving()

        _logger.LogInformation("AS4 Agent {Name} Started!", AgentConfig.Name);
    }

    /// <summary>
    /// Stops this agent.
    /// </summary>
    /// <param name="cancellationToken">The cancellation.</param>
    /// <returns></returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Stopping AS4 Agent {Name} ...", AgentConfig.Name);
        _receiver.StopReceiving();

        await base.StopAsync(cancellationToken);

        _logger.LogInformation("AS4 Agent {Name} stopped.", AgentConfig.Name);
    }

    protected async Task<MessagingContext> OnReceived(ReceivedMessage message, CancellationToken cancellation)
    {
        try
        {
            var context = await _transformer.TransformAsync(message, cancellation)
                ?? throw new ArgumentNullException(nameof(message),
                   $@"Transformer {_transformer.GetType().Name} result in a 'null', transformers require to transform into a 'MessagingContext'");
            if (context.ErrorResult != null)
            {
                return context;
            }

            var stepResult = await _steps.ExecuteStepsAsync(context, cancellation);
            var journalWithAgentLocation = stepResult.Journal.Select(j =>
            {
                j.AddAgentLocation(AgentConfig);
                return j;
            });

            await _journalLogger.WriteLogEntriesAsync(journalWithAgentLocation, cancellation);
            return stepResult.MessagingContext;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not transform message: {Message}", exception.Message);

            return await _exceptionHandler.HandleTransformationExceptionAsync(exception, message, cancellation);
        }
    }
}
