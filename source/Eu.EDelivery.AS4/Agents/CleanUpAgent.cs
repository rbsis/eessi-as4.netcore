using Eu.EDelivery.AS4.Common;
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
public class CleanUpAgent : IAgent, IDisposable
{
    private readonly ILogger<CleanUpAgent> _logger;
    private readonly IDbContextFactory<DatastoreContext> _contextFactory;
    private readonly TimeSpan _retentionPeriod;

    private Timer? _timer = null;
    private bool _disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanUpAgent" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="contextFactory">The context factory.</param>
    /// <param name="config">The configuration whit the retention period.</param>
    public CleanUpAgent(ILogger<CleanUpAgent> logger, IDbContextFactory<DatastoreContext> contextFactory, IConfig config)
    {
        _logger = logger;
        _contextFactory = contextFactory;
        _retentionPeriod = config.RetentionPeriod;
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
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("Starting {Name}...", AgentConfig.Name);
        _logger.LogDebug("Will clean up entries older than: \"{RetentionPeriod}\"", DateTimeOffset.Now.Subtract(_retentionPeriod));

        _timer = new Timer(StartCleaningMessagesTables, null, TimeSpan.Zero, TimeSpan.FromDays(1));

        _logger.LogInformation("{Name} Started!", AgentConfig.Name);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops this agent.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Stopping {Name} ...", AgentConfig.Name);

        _timer?.Change(Timeout.Infinite, 0);

        _logger.LogInformation("{Name} stopped.", AgentConfig.Name);

        return Task.CompletedTask;
    }

    private void StartCleaningMessagesTables(object? state)
    {
        try
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
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Clean messages tables failed");
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _timer?.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
