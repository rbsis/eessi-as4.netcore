using System.Data;
using System.Linq.Dynamic.Core;
using Eu.EDelivery.AS4.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Strategies.Database;

internal class SqlServerDbCommand : IAS4DbCommand
{
    private readonly DatastoreContext _context;
    private readonly ILogger<SqlServerDbCommand> _logger;

    // TODO: this is kind of similiar to the 'DatastoreTable' class
    private readonly IDictionary<string, Func<DatastoreContext, IQueryable<Entity>>> _tablesByName =
        new Dictionary<string, Func<DatastoreContext, IQueryable<Entity>>>
        {
            ["InMessages"] = c => c.InMessages.FromSqlRaw(CreateSqlStatement("InMessages")),
            ["OutMessages"] = c => c.OutMessages.FromSqlRaw(CreateSqlStatement("OutMessages")),
            ["InExceptions"] = c => c.InExceptions.FromSqlRaw(CreateSqlStatement("InExceptions")),
            ["OutExceptions"] = c => c.OutExceptions.FromSqlRaw(CreateSqlStatement("OutExceptions")),
            ["RetryReliability"] = c => c.RetryReliability.FromSqlRaw(CreateSqlStatement("RetryReliability"))
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerDbCommand" /> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger"></param>
    public SqlServerDbCommand(DatastoreContext context, ILogger<SqlServerDbCommand> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets the exclusive lock isolation for the transaction of retrieval of entities.
    /// </summary>
    /// <value>The exclusive lock isolation.</value>
    public IsolationLevel? ExclusiveLockIsolation => new IsolationLevel?();

    /// <summary>
    /// Initialization process for the different DBMS storage types.
    /// </summary>
    public void CreateDatabase()
    {
        _context.Database.Migrate();
    }

    /// <summary>
    /// Initialization process for the different DBMS storage types.
    /// </summary>
    public async Task CreateDatabaseAsync(CancellationToken cancellation)
    {
        await _context.Database.MigrateAsync(cancellation);
    }

    /// <summary>
    /// Exclusively retrieves the entities.
    /// </summary>
    /// <param name="tableName">Name of the Db table.</param>
    /// <param name="filter">Order by this field.</param>
    /// <param name="takeRows">Take this amount of rows.</param>
    /// <returns></returns>
    public IEnumerable<Entity> ExclusivelyRetrieveEntities(string tableName, string filter, int takeRows)
    {
        if (!(DatastoreTable.IsTableNameKnown(tableName) && _tablesByName.ContainsKey(tableName)))
        {
            throw new InvalidOperationException($"The configured table {tableName} could not be found");
        }

        return _tablesByName[tableName](_context)
            .Where(filter.Replace("\'", "\""))
            .OrderBy(x => x.InsertionTime)
            .Take(takeRows)
            .ToList();
    }

    private static string CreateSqlStatement(string tableName)
    {
        return $"SELECT * FROM {tableName} WITH (xlock, readpast)";
    }

    /// <summary>
    /// Delete the Messages Entities that are inserted passed a given <paramref name="retentionPeriod"/> 
    /// and has a <see cref="Operation"/> within the given <paramref name="allowedOperations"/>.
    /// </summary>
    /// <param name="tableName"></param>
    /// <param name="retentionPeriod">The retention period.</param>
    /// <param name="allowedOperations">The allowed operations.</param>
    public void BatchDeleteOverRetentionPeriod(
        string tableName,
        TimeSpan retentionPeriod,
        IEnumerable<Operation> allowedOperations)
    {
        DatastoreTable.EnsureTableNameIsKnown(tableName);

        var operations = string.Join(", ", allowedOperations.Select(x => "'" + x.ToString() + "'"));
        var outMessagesWhere =
            tableName.Equals("OutMessages")
                ? @" AND (
                                (m.EbmsMessageType = 'UserMessage' AND m.Status IN('Ack', 'Nack')) 
                                OR m.EbmsMessageType IN('Receipt', 'Error')
                             )"
                : string.Empty;

        var sql =
            $"DELETE m FROM {tableName} m " +
            $"WHERE m.InsertionTime < GETDATE() - {retentionPeriod.TotalDays:##.##} " +
            $"AND Operation IN ({operations})" +
            outMessagesWhere;

#pragma warning disable EF1000 // Possible SQL injection vulnerability.
        // The DatastoreTable makes sure that we only use known table names.
        // The list of Operation enums makes sure that only use Operation values.
        // The TotalDays of the TimeSpan is an integer.
        var rows = _context.Database.ExecuteSqlRaw(sql);
#pragma warning restore EF1000 // Possible SQL injection vulnerability.

        _logger.LogTrace("Cleaned {Rows} row(s) for table '{TableName}'", rows, tableName);
    }

    /// <summary>
    /// Selects in a reliable way the ToBePiggyBacked SignalMessages stored in the OutMessage table.
    /// </summary>
    /// <param name="url">The endpoint to which the OutMessage SignalMessage should be Piggy Backed.</param>
    /// <param name="mpc">The MPC of the incoming PullRequest to match on the related UserMessage of the Piggy Backed SignalMessage.</param>
    /// <returns></returns>
    public IEnumerable<OutMessage> SelectToBePiggyBackedSignalMessages(string url, string mpc)
    {
        // TODO: the 'TOP' of the query should be configurable.
        const string Sql =
            "SELECT TOP 10 OutMessages.* "
            + "FROM OutMessages WITH (xlock, readpast) "
            + "INNER JOIN InMessages "
            + "ON OutMessages.EbmsRefToMessageId = InMessages.EbmsMessageId "
            + "WHERE OutMessages.Operation = 'ToBePiggyBacked' "
            + "AND OutMessages.URL = {0} "
            + "AND InMessages.MPC = {1} "
            + "AND OutMessages.EbmsMessageType != 'UserMessage' "
            + "ORDER BY OutMessages.InsertionTime ASC ";

        return _context.OutMessages
                       .FromSqlRaw(Sql, url, mpc)
                       .AsEnumerable<OutMessage>();
    }
}
