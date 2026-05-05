using Eu.EDelivery.AS4.Repositories;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Services.Journal;

/// <summary>
/// Journal logger implementation that writes journal entries to the datastore.
/// </summary>
public class JournalDatastoreLogger : IJournalLogger
{
    private readonly ILogger<JournalDatastoreLogger> _logger;
    private readonly IDatastoreRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="JournalDatastoreLogger"/> class.
    /// </summary>
    public JournalDatastoreLogger(ILogger<JournalDatastoreLogger> logger, IDatastoreRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    /// <summary>
    /// Writes out a given journal log <paramref name="entries"/>.
    /// </summary>
    /// <param name="entries">The entries that must be written.</param>
    /// <param name="cancellation"></param>
    public async Task WriteLogEntriesAsync(IEnumerable<JournalLogEntry> entries, CancellationToken cancellation)
    {
        var entities = entries.Select(CreateJournalRecord);

        await _repository.InsertJournalsAsync(entities, cancellation);
    }

    private Entities.Journal CreateJournalRecord(JournalLogEntry entry)
    {
        var entity = new Entities.Journal
        {
            EbmsMessageId = entry.EbmsMessageId,
            RefToEbmsMessageId = entry.RefToMessageId,
            Action = entry.Action,
            Service = entry.Service,
            FromParty = entry.FromParty,
            ToParty = entry.ToParty,
            LogEntry = string.Join(Environment.NewLine, entry.LogEntries),
            LogDate = DateTimeOffset.Now,
            AgentName = entry.AgentName ?? string.Empty,
            AgentType = entry.AgentType.HasValue ? entry.AgentType.Value.ToString() : string.Empty
        };

        var ebmsMessageId = entry.EbmsMessageId == null
            ? string.Empty
            : "EbmsMessageId=" + entry.EbmsMessageId;

        var refToMessageId = entry.RefToMessageId == null
            ? string.Empty
            : "RefToMessageId=" + entry.RefToMessageId;

        _logger.LogTrace("Add Journal entry for message {MessageId}", string.Join(", ", ebmsMessageId, refToMessageId));
        return entity;
    }
}
