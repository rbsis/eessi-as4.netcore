using System.ComponentModel;
using EnsureThat;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Fe.Exceptions;
using Eu.EDelivery.AS4.Fe.Mappers;
using Eu.EDelivery.AS4.Fe.Monitor.Model;
using Eu.EDelivery.AS4.Fe.Pmodes;
using Eu.EDelivery.AS4.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eu.EDelivery.AS4.Fe.Services;

/// <summary>
/// Service to view messages
/// </summary>
/// <seealso cref="IMonitorService" />
public class MonitorService : IMonitorService
{
    private readonly IDbContextFactory<DatastoreContext> _contextFactory;
    private readonly IAs4PmodeSource _pmodeSource;
    private readonly IDatastoreRepository _datastoreRepository;
    private readonly IAS4MessageBodyStore _bodyStore;
    private readonly IMapper<InMessage, Message> _inMessageMapper;
    private readonly IMapper<OutMessage, Message> _outMessageMapper;
    private readonly IMapper<InException, ExceptionMessage> _inExceptionMapper;
    private readonly IMapper<OutException, ExceptionMessage> _outExceptionMapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="MonitorService" /> class.
    /// </summary>
    /// <param name="contextFactory">The context factory.</param>
    /// <param name="pmodeSource">The pmode source.</param>
    /// <param name="datastoreRepository">The datastore repository.</param>
    /// <param name="bodyStore">The body store.</param>
    /// <param name="inMessageMapper"></param>
    /// <param name="outMessageMapper"></param>
    /// <param name="inExceptionMapper"></param>
    /// <param name="outExceptionMapper"></param>
    public MonitorService(
        IDbContextFactory<DatastoreContext> contextFactory,
        IAs4PmodeSource pmodeSource,
        IDatastoreRepository datastoreRepository,
        IAS4MessageBodyStore bodyStore,
        IMapper<InMessage, Message> inMessageMapper,
        IMapper<OutMessage, Message> outMessageMapper,
        IMapper<InException, ExceptionMessage> inExceptionMapper,
        IMapper<OutException, ExceptionMessage> outExceptionMapper)
    {
        _contextFactory = contextFactory;
        _pmodeSource = pmodeSource;
        _datastoreRepository = datastoreRepository;
        _bodyStore = bodyStore;
        _inMessageMapper = inMessageMapper;
        _outMessageMapper = outMessageMapper;
        _inExceptionMapper = inExceptionMapper;
        _outExceptionMapper = outExceptionMapper;
    }

    /// <summary>
    /// Gets the exceptions.
    /// </summary>
    /// <param name="filter">Exception filter object</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">filter - Filter must be supplied
    /// or
    /// Direction - Direction cannot be null</exception>
    /// <exception cref="BusinessException">Could not get any exceptions, something went wrong.</exception>
    public async Task<MessageResult<ExceptionMessage>> GetExceptionsAsync(ExceptionFilter? filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter, nameof(filter));
        ArgumentNullException.ThrowIfNull(filter.Direction, nameof(filter.Direction));

        using var datastoreContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var inExceptions = filter.Direction.Contains(Direction.Inbound) ? filter.ApplyFilter(datastoreContext.InExceptions).Select(exception => _inExceptionMapper.Map(exception)) : null;
        var outExceptions = filter.Direction.Contains(Direction.Outbound) ? filter.ApplyFilter(datastoreContext.OutExceptions).Select(exception => _outExceptionMapper.Map(exception)) : null;

        IQueryable<ExceptionMessage>? result = null;
        if (inExceptions != null && outExceptions != null) result = inExceptions.Concat(outExceptions);
        else if (inExceptions != null) result = inExceptions;
        else if (outExceptions != null) result = outExceptions;

        if (result == null) throw new BusinessException("Could not get any exceptions, something went wrong.");

        return await filter.ToResult(result.OrderByDescending(msg => msg.InsertionTime));
    }

    /// <summary>
    /// Gets the messages.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">
    /// filter - Filter cannot be null
    /// or
    /// Direction - Direction filter cannot be empty
    /// </exception>
    /// <exception cref="BusinessException">No messages found</exception>
    public async Task<MessageResult<Message>> GetMessagesAsync(MessageFilter? filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter, nameof(filter));
        ArgumentNullException.ThrowIfNull(filter.Direction, nameof(filter.Direction));

        using var datastoreContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<InMessage> inMessageQuery = datastoreContext.InMessages;
        IQueryable<OutMessage> outMessageQuery = datastoreContext.OutMessages;

        var inMessages = filter.Direction.Contains(Direction.Inbound) ? filter.ApplyFilter(inMessageQuery).Select(message => _inMessageMapper.Map(message)) : null;
        var outMessages = filter.Direction.Contains(Direction.Outbound) ? filter.ApplyFilter(outMessageQuery).Select(message => _outMessageMapper.Map(message)) : null;

        IQueryable<Message>? result = null;
        if (inMessages != null && outMessages != null) result = inMessages.Concat(outMessages);
        else if (inMessages != null) result = inMessages;
        else if (outMessages != null) result = outMessages;
        if (result == null) throw new BusinessException("No messages found");

        var returnValue = await filter.ToResult(filter.ApplyStatusFilter(result).OrderByDescending(msg => msg.InsertionTime));
        UpdateHasExceptions(returnValue, await GetExceptionIdsAsync(returnValue, cancellationToken));

        return returnValue;
    }

    /// <summary>
    /// Gets the related messages.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<MessageResult<Message>> GetRelatedMessagesAsync(Direction direction, string messageId, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNullOrEmpty(messageId);

        using var datastoreContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var refToMessageId = direction == Direction.Inbound
            ? datastoreContext.InMessages.Where(message => message.EbmsMessageId == messageId).Select(message => message.EbmsRefToMessageId).FirstOrDefault()
            : datastoreContext.OutMessages.Where(message => message.EbmsMessageId == messageId).Select(message => message.EbmsRefToMessageId).FirstOrDefault();

        var resultTest = new List<IQueryable<Message>>();

        if (!string.IsNullOrEmpty(refToMessageId))
        {
            resultTest.Add(datastoreContext.InMessages
                .Where(message => message.EbmsMessageId == refToMessageId)
                .Select(message => _inMessageMapper.Map(message)));

            resultTest.Add(datastoreContext.OutMessages
                .Where(message => message.EbmsMessageId == refToMessageId)
                .Select(message => _outMessageMapper.Map(message)));
        }

        if (!string.IsNullOrEmpty(messageId))
        {
            resultTest.Add(datastoreContext.InMessages
                .Where(message => message.EbmsRefToMessageId == messageId)
                .Select(message => _inMessageMapper.Map(message)));

            resultTest.Add(datastoreContext.OutMessages
                .Where(message => message.EbmsRefToMessageId == messageId)
                .Select(message => _outMessageMapper.Map(message)));

            if (direction == Direction.Inbound)
            {
                resultTest.Add(datastoreContext.OutMessages
                    .Where(message => message.EbmsMessageId == messageId)
                    .Select(message => _outMessageMapper.Map(message)));
            }
            else
            {
                resultTest.Add(datastoreContext.InMessages
                    .Where(message => message.EbmsMessageId == messageId)
                    .Select(message => _inMessageMapper.Map(message)));
            }
        }

        var result = resultTest.First();
        result = resultTest.Skip(1).Aggregate(result, (current, query) => current.Union(query));

        return new MessageResult<Message>
        {
            Messages = await result.ToListAsync(cancellationToken),
            Total = await result.CountAsync(cancellationToken),
            Page = 0,
            Pages = 0,
            CurrentPage = 0
        };
    }

    /// <summary>
    /// Gets the pmode number.
    /// </summary>
    /// <param name="pmode">The pmode.</param>
    /// <returns></returns>
    public string GetPmodeNumber(string pmode)
    {
        return string.IsNullOrEmpty(pmode) ? string.Empty : _pmodeSource.GetPmodeNumber(pmode) ?? string.Empty;
    }

    /// <summary>
    /// Downloads the message body.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">messageId - messageId parameter cannot be null</exception>
    /// <exception cref="InvalidEnumArgumentException">direction</exception>
    public async Task<Stream> DownloadMessageBodyAsync(Direction direction, long id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), @"Invalid value for id");
        }

        if (!Enum.IsDefined(typeof(Direction), direction))
        {
            throw new InvalidEnumArgumentException(nameof(direction), (int)direction, typeof(Direction));
        }

        var body = await (direction == Direction.Inbound
            ? _datastoreRepository.GetInMessageData(m => m.Id == id, x => (MessageEntity)x)
            : _datastoreRepository.GetOutMessageData(m => m.Id == id, x => x))
            .Single()
            .RetrieveMessageBodyAsync(_bodyStore, cancellationToken);

        return body ?? throw new InvalidOperationException("Message body not found");
    }

    /// <summary>
    /// Downloads the exception body.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The exception</returns>
    /// <exception cref="ArgumentNullException">messageId - messageId parameter cannot be null</exception>
    public async Task<Stream> DownloadExceptionMessageBodyAsync(Direction direction, long id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), @"Invalid value for id");
        }

        if (!Enum.IsDefined(typeof(Direction), direction))
        {
            throw new InvalidEnumArgumentException(nameof(direction), (int)direction, typeof(Direction));
        }

        string? body;
        if (direction == Direction.Inbound)
        {
            using var datastoreContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            body = await datastoreContext.InExceptions
                .Where(msg => msg.Id == id)
                .Select(msg => msg.MessageLocation)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            using var datastoreContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            body = await datastoreContext.OutExceptions
                .Where(msg => msg.Id == id)
                .Select(msg => msg.MessageLocation)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await _bodyStore.LoadMessageBodyAsync(body, cancellationToken)
            ?? throw new InvalidOperationException("Exception body not found");
    }

    /// <summary>
    /// Gets the exception detail.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<string?> GetExceptionDetailAsync(Direction direction, long messageId, CancellationToken cancellationToken)
    {
        using var datastoreContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        if (direction == Direction.Inbound)
        {
            return await datastoreContext.InExceptions
                .Where(x => x.Id == messageId)
                .Select(x => x.Exception)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await datastoreContext.OutExceptions
            .Where(x => x.Id == messageId)
            .Select(x => x.Exception)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static void UpdateHasExceptions(MessageResult<Message> returnValue, List<string?> exceptionIds)
    {
        returnValue.Messages = returnValue.Messages.Select(x =>
        {
            x.HasExceptions = exceptionIds.Any(ex => ex == x.EbmsMessageId);
            return x;
        });
    }

    private async Task<List<string?>> GetExceptionIdsAsync(MessageResult<Message> returnValue, CancellationToken cancellationToken)
    {
        var ids = returnValue.Messages.Select(msg => msg.EbmsMessageId).ToList();

        using var datastoreContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var inExceptions = datastoreContext.InExceptions
            .Where(ex => ids.Contains(ex.EbmsRefToMessageId))
            .Select(ex => ex.EbmsRefToMessageId);

        var outExceptions = datastoreContext.OutExceptions
            .Where(ex => ids.Contains(ex.EbmsRefToMessageId))
            .Select(ex => ex.EbmsRefToMessageId);

        return await inExceptions
            .Union(outExceptions)
            .ToListAsync(cancellationToken);
    }
}
