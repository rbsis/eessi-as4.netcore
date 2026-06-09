using Microsoft.EntityFrameworkCore;

namespace Eu.EDelivery.AS4.Fe.Database;

public static class SqlConnectionBuilder
{
    public static void Build(string provider, string connectionString, DbContextOptionsBuilder builder)
    {
        if (provider.Equals("sqlite", StringComparison.CurrentCultureIgnoreCase))
        {
            builder.UseSqlite(connectionString);
            return;
        }
        else if (provider.Equals("sqlserver", StringComparison.CurrentCultureIgnoreCase))
        {
            builder.UseSqlServer(connectionString);
            return;
        }
        throw new Exception($"No provider found for {provider}");
    }
}
