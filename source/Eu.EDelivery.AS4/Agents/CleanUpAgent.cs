using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Strategies.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Agents;

/// <summary>
/// <see cref="IAgent"/> implementation that runs a Clean Up job every day.
/// This job consists of deleting messages that are inserted older that the given retention period (local configuration settings specifies this in days).
/// </summary>
/// <seealso cref="IAgent" />
internal class CleanUpAgent : IAgent
{
    private readonly ILogger<CleanUpAgent> _logger;

    private readonly IDbContextFactory<DatastoreContext> _contextFactory;
    private readonly TimeSpan _retentionPeriod;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanUpAgent" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="contextFactory">The context factory.</param>
    /// <param name="retentionPeriod">The retention period.</param>
    public CleanUpAgent(ILogger<CleanUpAgent> logger, IDbContextFactory<DatastoreContext> contextFactory, TimeSpan retentionPeriod)
    {
        _logger = logger;
        _contextFactory = contextFactory;
        _retentionPeriod = retentionPeriod;
    }

    /// <summary>
    /// Gets the agent configuration.
    /// </summary>
    /// <value>The agent configuration.</value>
    public AgentConfig AgentConfig { get; } = new AgentConfig("Clean Up Agent");

    /// <summary>
    /// Starts the specified agent.
    /// </summary>
    /// <param name="cancellationToken">The cancellation.</param>
    /// <returns></returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("Starting {Name}...", AgentConfig.Name);
        _logger.LogDebug("Will clean up entries older than: \"{RetentionPeriod}\"", DateTimeOffset.Now.Subtract(_retentionPeriod));

        try
        {
            await Observable.Interval(TimeSpan.FromDays(1), TaskPoolScheduler.Default)
                .StartWith(0)
                .Do(_ => StartCleaningMessagesTables())
                .ToTask(cancellationToken);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "{Name} Stopped!", AgentConfig.Name);
        }
    }

    /// <summary>
    /// Stops this agent.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void StartCleaningMessagesTables()
    {
        using var context = _contextFactory.CreateDbContext();
        var allowedOperations = new[]
        {
            Operation.Delivered,
            Operation.Forwarded,
            Operation.Notified,
            Operation.Sent,
            Operation.NotApplicable,
            Operation.Undetermined
        };

        foreach (var table in DatastoreTable.DomainEntityTables)
        {
            context.NativeCommands.BatchDeleteOverRetentionPeriod(table, _retentionPeriod, allowedOperations);
        }
    }
}
