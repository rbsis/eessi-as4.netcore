using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Eu.EDelivery.AS4.Entities;

internal class DatastoreContextMigrator : IHostedService
{
    private readonly IDbContextFactory<DatastoreContext> _contextFactory;

    public DatastoreContextMigrator(IDbContextFactory<DatastoreContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var context = _contextFactory.CreateDbContext();
        await context.NativeCommands.CreateDatabaseAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
