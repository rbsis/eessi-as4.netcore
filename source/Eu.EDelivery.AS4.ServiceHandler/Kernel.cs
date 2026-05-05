using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.ServiceHandler.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.ServiceHandler;

/// <summary>
/// Start point for AS4 Connection
/// Wrapper for the Channels
/// </summary>
public sealed class Kernel : IDisposable
{
    private readonly ILogger<Kernel> _logger;
    private readonly IEnumerable<IAgent> _agents;
    private readonly IDbContextFactory<DatastoreContext> _contextFactory;
    private bool _disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="Kernel" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="agentsProvider">The agents provider.</param>
    /// <param name="contextFactory"></param>
    public Kernel(
        ILogger<Kernel> logger,
        AgentProvider agentsProvider,
        IDbContextFactory<DatastoreContext> contextFactory)
    {
        _logger = logger;
        _agents = agentsProvider.Agents;
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Starting Kernel > starting all Agents
    /// </summary>
    /// <param name="cancellationToken">Cancel the Kernel if needed</param>
    /// <returns></returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_agents.Any())
        {
            _logger.LogWarning("Will not start Kernel: no IAgent implementations has been set to the Kernel");
            return;
        }

        try
        {
            using var context = _contextFactory.CreateDbContext();
            await context.NativeCommands.CreateDatabaseAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "An error occured while migrating the database");
            return;

        }

        _logger.LogTrace("Starting...");
        var task = Task.WhenAll(_agents.Select(c => c.StartAsync(cancellationToken)).ToArray());
        _logger.LogTrace("Started!");

        await task;
    }

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                foreach (var agent in _agents)
                {
                    var disposableAgent = agent as IDisposable;
                    disposableAgent?.Dispose();
                }
            }

            _disposedValue = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
