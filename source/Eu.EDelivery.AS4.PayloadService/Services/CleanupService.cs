using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Eu.EDelivery.AS4.PayloadService.Persistance;

namespace Eu.EDelivery.AS4.PayloadService.Services;

/// <summary>
/// Service to run periodically to cleaning up the retired persisted payloads.
/// </summary>
public class CleanUpService : BackgroundService
{
    private readonly ILogger<CleanUpService> _logger;
    private readonly IPayloadPersister _payloadPersister;
    private readonly TimeSpan _retentionPeriod;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanUpService" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="payloadPersister">The payload persister.</param>
    /// <param name="configuration"></param>
    public CleanUpService(ILogger<CleanUpService> logger, IPayloadPersister payloadPersister, IConfiguration configuration)
    {
        _logger = logger;
        _payloadPersister = payloadPersister;
        _retentionPeriod = TimeSpan.FromDays(configuration.GetValue("RetentionPeriod", defaultValue: 90));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) => await Observable
        .Interval(TimeSpan.FromDays(1))
        .StartWith(0)
        .Do(_ =>
        {
            _payloadPersister.CleanupPayloadsOlderThan(_retentionPeriod);
            _logger.LogTrace("Clean up payloads older than: {RetentionPeriod}", DateTimeOffset.UtcNow.Subtract(_retentionPeriod));
        })
        .Repeat()
        .ToTask(stoppingToken);
}
