using System.Configuration;
using System.Linq.Expressions;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Repositories;

/// <summary>
/// High level repository to use the Data store in a uniform way
/// </summary>
public class DatastoreRepository : IDatastoreRepository
{
    private readonly ILogger<DatastoreRepository> _logger;
    private readonly IDbContextFactory<DatastoreContext> _contextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatastoreRepository"/> class. 
    /// Create a high level Repository with a given Data store Context
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="contextFactory">
    /// </param>     
    public DatastoreRepository(
        ILogger<DatastoreRepository> logger,
        IDbContextFactory<DatastoreContext> contextFactory)
    {
        _logger = logger;
        _contextFactory = contextFactory;
    }

    #region InMessage related functionality

    /// <summary>
    /// Verifies whether there exists an InMessage entity that conforms to the specified predicate.
    /// </summary>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public bool InMessageExists(Expression<Func<InMessage, bool>> predicate)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.InMessages.Any(predicate);
    }

    /// <summary>
    /// Selects the in messages.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="messageId"></param>
    /// <param name="selection">The selection.</param>
    /// <returns></returns>
    public TResult? GetInMessageData<TResult>(string messageId, Expression<Func<InMessage, TResult>> selection)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.InMessages
            .Where(m => m.EbmsMessageId.Equals(messageId))
            .Select(selection)
            .FirstOrDefault();
    }

    /// <summary>
    /// Retrieves information for specified InMessages.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="where">The where.</param>
    /// <param name="selection">The selection.</param>
    /// <returns></returns>s
    public IEnumerable<TResult> GetInMessageData<TResult>(Expression<Func<InMessage, bool>> where, Expression<Func<InMessage, TResult>> selection)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.InMessages
            .Where(where)
            .Select(selection)
            .ToList();
    }

    /// <summary>
    /// Retrieves information for a <see cref="InMessage"/> for a given <paramref name="messageId"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result to return</typeparam>
    /// <param name="messageId">The identifier to locate the <see cref="InMessage"/></param>
    /// <param name="selection">The selector function to manipulate the <typeparamref name="TResult"/> type</param>
    /// <returns></returns>
    public TResult? GetInMessageData<TResult>(long messageId, Expression<Func<InMessage, TResult>> selection)
    {
        return GetInMessageData(m => m.Id == messageId, selection).SingleOrDefault();
    }

    /// <summary>
    /// Selects some information of specified InMessages.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="messageIds"></param>
    /// <param name="selection"></param>
    /// <returns></returns>
    public IEnumerable<TResult> GetInMessagesData<TResult>(IEnumerable<string> messageIds, Expression<Func<InMessage, TResult>> selection)
    {
        if (!messageIds.Any())
        {
            return [];
        }

        using var context = _contextFactory.CreateDbContext();
        return context.InMessages
            .Where(m => messageIds.Contains(m.EbmsMessageId))
            .Select(selection)
            .ToList();
    }

    /// <summary>
    /// Select all the found 'EbmsMessageIds' in the given datastore.
    /// </summary>
    /// <param name="searchedMessageIds">Collection of 'EbmsMessageIds' to be search for.</param>
    /// <returns></returns>
    public IEnumerable<string> SelectExistingInMessageIds(IEnumerable<string> searchedMessageIds)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.InMessages
            .Where(m => searchedMessageIds.Contains(m.EbmsMessageId))
            .Select(m => m.EbmsMessageId)
            .ToList();
    }

    /// <summary>
    /// Search all the found 'RefToMessageIds' in the given datastore.
    /// </summary>
    /// <param name="searchedMessageIds"></param>
    /// <returns></returns>
    public IEnumerable<string> SelectExistingInRefToMessageIds(IEnumerable<string> searchedMessageIds)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.InMessages
            .Where(m => m.EbmsRefToMessageId != null && searchedMessageIds.Contains(m.EbmsRefToMessageId))
            .Select(m => m.EbmsRefToMessageId!)
            .ToList();
    }

    /// <summary>
    /// Insert a given <see cref="InMessage"/> into the Data store
    /// </summary>
    /// <param name="inMessage"></param>
    public void InsertInMessage(InMessage inMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inMessage.MessageLocation);

        inMessage.InsertionTime = DateTimeOffset.Now;
        inMessage.ModificationTime = DateTimeOffset.Now;

        using var context = _contextFactory.CreateDbContext();
        context.InMessages.Add(inMessage);

        context.SaveChanges();
    }

    /// <summary>
    /// Updates a <see cref="InMessage"/> using a given <paramref name="update"/> function.
    /// </summary>
    /// <param name="id">The identifier to locate the <see cref="InMessage"/></param>
    /// <param name="update">The update function to change the located <see cref="InMessage"/></param>
    public void UpdateInMessage(long id, Action<InMessage> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        using var context = _contextFactory.CreateDbContext();
        var entity = context.InMessages.Single(m => m.Id == id);

        update(entity);

        entity.ModificationTime = DateTimeOffset.Now;
        context.SaveChanges();
    }

    /// <summary>
    /// Update a found InMessage (by AS4 Message Id) in the Data store
    /// </summary>
    /// <param name="messageId"></param>
    /// <param name="updateAction"></param>
    public void UpdateInMessage(string? messageId, Action<InMessage> updateAction)
    {
        ArgumentException.ThrowIfNullOrEmpty(messageId);

        // There might exist multiple InMessage records for the given messageId, therefore we cannot use
        // caching here.            
        using var context = _contextFactory.CreateDbContext();
        var inMessageIds = context.InMessages
            .Where(m => m.EbmsMessageId.Equals(messageId))
            .Select(m => m.Id)
            .ToArray();

        foreach (var id in inMessageIds)
        {
            var message = GetInMessageEntityFor(context, id);
            if (message == null)
            {
                _logger.LogWarning("Unable to update InMessage {MessageId}.  There exists no such InMessage.", messageId);
                return;
            }

            updateAction(message);
            message.ModificationTime = DateTimeOffset.Now;
        }

        context.SaveChanges();
    }

    /// <summary>
    /// Updates a set of <see cref="InMessage"/> entities using a <paramref name="updateAction"/> function 
    /// for which the given <paramref name="predicate"/> holds.
    /// </summary>
    /// <param name="predicate">The predicate function to locate a set of <see cref="InMessage"/> entities</param>
    /// <param name="updateAction">The update function to change the located <see cref="InMessage"/> entities</param>
    public void UpdateInMessages(Expression<Func<InMessage, bool>> predicate, Action<InMessage> updateAction)
    {
        using var context = _contextFactory.CreateDbContext();
        var inMessageIds = context.InMessages.Where(predicate).Select(m => new { m.EbmsMessageId, m.Id }).ToArray();

        if (inMessageIds.Any())
        {
            foreach (var idSet in inMessageIds)
            {
                var message = GetInMessageEntityFor(context, idSet.Id);
                if (message != null)
                {
                    updateAction(message);
                    message.ModificationTime = DateTimeOffset.Now;
                }
            }
        }

        context.SaveChanges();
    }

    private InMessage? GetInMessageEntityFor(DatastoreContext context, long id)
    {
        var msg = context.InMessages.FirstOrDefault(m => m.Id == id);
        if (msg == null)
        {
            _logger.LogError("No InMessage found for MessageId {Id}", id);
            return null;
        }

        return msg;
    }

    #endregion

    #region OutMessage related functionality

    /// <summary>
    /// Determines whether any stored <see cref="OutMessage"/> satisfies a given <paramref name="predicate"/>.
    /// </summary>
    /// <param name="predicate">The predicate function used in the determination</param>
    /// <returns></returns>
    public bool OutMessageExists(Expression<Func<OutMessage, bool>> predicate)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.OutMessages.Any(predicate);
    }

    /// <summary>
    /// Retrieves information for a single <see cref="OutMessage"/> for a given <paramref name="messageId"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of result to return</typeparam>
    /// <param name="messageId">The identifier to locate the <see cref="OutMessage"/></param>
    /// <param name="selection">The selector function to manipulate the <typeparamref name="TResult"/> type</param>
    /// <returns></returns>
    public TResult? GetOutMessageData<TResult>(long messageId, Expression<Func<OutMessage, TResult>> selection) =>
        GetOutMessageData(m => m.Id == messageId, selection).SingleOrDefault();

    /// <summary>
    /// Retrieves information for specified OutMessages.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="where">The where.</param>
    /// <param name="selection">The selection.</param>
    /// <returns></returns>
    public IEnumerable<TResult> GetOutMessageData<TResult>(Expression<Func<OutMessage, bool>> where, Expression<Func<OutMessage, TResult>> selection)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.OutMessages
            .Where(where)
            .Select(selection)
            .ToList();
    }

    /// <summary>
    /// Insert a given <see cref="OutMessage"/>
    /// into the Data store
    /// </summary>
    /// <param name="outMessage"></param>        
    public void InsertOutMessage(OutMessage outMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outMessage.MessageLocation);

        outMessage.InsertionTime = DateTimeOffset.Now;
        outMessage.ModificationTime = DateTimeOffset.Now;

        using var context = _contextFactory.CreateDbContext();
        context.OutMessages.Add(outMessage);

        context.SaveChanges();
    }

    /// <summary>
    /// Update a found OutMessage (by AS4 Message Id) in the Data store.
    /// </summary>
    /// <param name="outMessageId"></param>
    /// <param name="updateAction"></param>
    public void UpdateOutMessage(long outMessageId, Action<OutMessage> updateAction)
    {
        using var context = _contextFactory.CreateDbContext();
        var msg = GetOutMessageEntityFor(context, outMessageId);
        UpdateMessageEntityIfNotNull(updateAction, msg);

        context.SaveChanges();
    }

    /// <summary>
    /// Updates a set of <see cref="OutMessage"/> entities using a given <paramref name="updateAction"/>
    /// for which the given <paramref name="predicate"/> holds.
    /// </summary>
    /// <param name="predicate">The predicate function to locate the <see cref="OutMessage"/> entities</param>
    /// <param name="updateAction">The update function to change the located <see cref="OutMessage"/> entities</param>
    public void UpdateOutMessages(Expression<Func<OutMessage, bool>> predicate, Action<OutMessage> updateAction)
    {
        ArgumentNullException.ThrowIfNull(updateAction);

        using var context = _contextFactory.CreateDbContext();
        var keys = context.OutMessages.Where(predicate).Select(m => new { m.Id }).ToArray();

        foreach (var key in keys)
        {
            var msg = GetOutMessageEntityFor(context, key.Id);
            UpdateMessageEntityIfNotNull(updateAction, msg);
        }

        context.SaveChanges();
    }

    private OutMessage? GetOutMessageEntityFor(DatastoreContext context, long outMessageId)
    {
        var msg = context.OutMessages.FirstOrDefault(m => m.Id == outMessageId);
        if (msg == null)
        {
            _logger.LogError("No OutMessage found for OutMessageId {OutMessageId}", outMessageId);
            return null;
        }

        return msg;
    }

    private static void UpdateMessageEntityIfNotNull(Action<OutMessage> updateAction, OutMessage? msg)
    {
        if (msg != null)
        {
            updateAction(msg);
            msg.ModificationTime = DateTimeOffset.Now;
        }
    }

    #endregion

    #region OutException related functionality

    /// <summary>
    /// Retrieves information for a single <see cref="OutException"/> entity for a given <paramref name="id"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of result to return</typeparam>
    /// <param name="id">The identifier to locate the <see cref="OutException"/> entity</param>
    /// <param name="selector">The selector function to manipulate the <typeparamref name="TResult"/> type</param>
    /// <returns></returns>s
    public TResult? GetOutExceptionData<TResult>(long id, Expression<Func<OutException, TResult>> selector)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.OutExceptions
            .Where(ex => ex.Id == id)
            .Select(selector)
            .SingleOrDefault();
    }

    /// <summary>
    /// Retrieves information for a specified OutException using a <paramref name="refToMessageId"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="refToMessageId"></param>
    /// <param name="selection">The selection.</param>
    /// <returns></returns>
    public IEnumerable<TResult> GetOutExceptionsData<TResult>(
        string refToMessageId,
        Expression<Func<OutException, TResult>> selection)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.OutExceptions
            .Where(ex => ex.EbmsRefToMessageId == refToMessageId)
            .Select(selection)
            .ToList();
    }

    /// <summary>
    /// Insert a given <see cref="OutException"/> into the Data store.
    /// </summary>
    /// <param name="outException"></param>
    public void InsertOutException(OutException outException)
    {
        outException.InsertionTime = DateTimeOffset.Now;
        outException.ModificationTime = DateTimeOffset.Now;

        using var context = _contextFactory.CreateDbContext();
        context.OutExceptions.Add(outException);

        context.SaveChanges();
    }

    /// <summary>
    /// Updates a single <see cref="OutException"/> entity for given <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The identifier to locate the <see cref="OutException"/> entity</param>
    /// <param name="update">The update function to change the located <see cref="OutException"/> entity</param>
    public void UpdateOutException(long id, Action<OutException> update)
    {
        using var context = _contextFactory.CreateDbContext();
        var entity = context.OutExceptions.Single(ex => ex.Id == id);

        update(entity);

        entity.ModificationTime = DateTimeOffset.Now;

        context.SaveChanges();
    }

    /// <summary>
    /// Update a found OutException (by AS4 Ref Message Id) in the Data store.
    /// </summary>
    /// <param name="refToMessageId"></param>
    /// <param name="updateAction"></param>
    public void UpdateOutException(string refToMessageId, Action<OutException> updateAction)
    {
        using var context = _contextFactory.CreateDbContext();
        var outExceptions = context.OutExceptions
            .Where(m => m.EbmsRefToMessageId != null && m.EbmsRefToMessageId.Equals(refToMessageId));

        foreach (var outException in outExceptions)
        {
            updateAction(outException);
            outException.ModificationTime = DateTimeOffset.Now;
        }

        context.SaveChanges();
    }

    #endregion

    #region InException functionality

    /// <summary>
    /// Retrieves information for a single <see cref="InException"/> for a given <paramref name="id"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of result to return</typeparam>
    /// <param name="id">The identifier to locate the <see cref="InException"/></param>
    /// <param name="selector">The selector function to manipulate the <typeparamref name="TResult"/> type</param>
    /// <returns></returns>
    public TResult? GetInExceptionData<TResult>(long id, Expression<Func<InException, TResult>> selector)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.InExceptions
            .Where(ex => ex.Id == id)
            .Select(selector)
            .SingleOrDefault();
    }

    /// <summary>
    /// Retrieves information for specified InException.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="refToMessageId"></param>
    /// <param name="selection">The selection.</param>
    /// <returns></returns>
    public IEnumerable<TResult> GetInExceptionsData<TResult>(
        string refToMessageId,
        Expression<Func<InException, TResult>> selection)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.InExceptions
            .Where(ex => ex.EbmsRefToMessageId == refToMessageId)
            .Select(selection)
            .ToList();
    }

    /// <summary>
    /// Insert a given <see cref="InException"/> into the Data store.</summary>
    /// <param name="inException"></param>
    public void InsertInException(InException inException)
    {
        inException.ModificationTime = DateTimeOffset.Now;
        inException.InsertionTime = DateTimeOffset.Now;

        using var context = _contextFactory.CreateDbContext();
        context.InExceptions.Add(inException);

        context.SaveChanges();
    }

    /// <summary>
    /// Updates a single <see cref="InException"/> for a given <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The identifier to locate the <see cref="InException"/> entity</param>
    /// <param name="update">The update function to change the <see cref="InException"/> entity</param>
    public void UpdateInException(long id, Action<InException> update)
    {
        using var context = _contextFactory.CreateDbContext();
        var entity = context.InExceptions.Single(ex => ex.Id == id);

        update(entity);

        entity.ModificationTime = DateTimeOffset.Now;

        context.SaveChanges();
    }

    /// <summary>
    /// Update a found InException (by AS4 Ref Message Id) in the Data store.
    /// </summary>
    /// <param name="refToMessageId"></param>
    /// <param name="updateAction"></param>
    public void UpdateInException(string refToMessageId, Action<InException> updateAction)
    {
        using var context = _contextFactory.CreateDbContext();
        var inExceptions = context.InExceptions
            .Where(m => m.EbmsRefToMessageId != null && m.EbmsRefToMessageId.Equals(refToMessageId));

        foreach (var inException in inExceptions)
        {
            updateAction(inException);
            inException.ModificationTime = DateTimeOffset.Now;
        }

        context.SaveChanges();
    }

    #endregion

    #region RetryReliability related functionality

    /// <summary>
    /// Gets a sequence of <see cref="RetryReliability"/> records based on a given <paramref name="predicate"/>,
    /// using a <paramref name="selector"/> to manipulate to a <typeparamref name="TResult"/> type.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="predicate"></param>
    /// <param name="selector"></param>
    /// <returns></returns>
    public IEnumerable<TResult> GetRetryReliability<TResult>(
        Expression<Func<RetryReliability, bool>> predicate,
        Expression<Func<RetryReliability, TResult>> selector)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.RetryReliability
            .Where(predicate)
            .Select(selector)
            .ToList();
    }

    /// <summary>
    /// Inserts the retry reliability information referencing a <see cref="InMessage"/>.
    /// </summary>
    /// <param name="reliability">The <see cref="RetryReliability"/> entity to insert</param>
    public void InsertRetryReliability(RetryReliability reliability)
    {
        reliability.InsertionTime = DateTimeOffset.Now;
        reliability.ModificationTime = DateTimeOffset.Now;

        using var context = _contextFactory.CreateDbContext();
        context.RetryReliability.Add(reliability);

        context.SaveChanges();
    }

    /// <summary>
    /// Inserts the retry reliability informations referencing <see cref="InMessage"/>'s.
    /// </summary>
    /// <param name="reliabilities">The <see cref="RetryReliability"/> entities to insert</param>
    public void InsertRetryReliabilities(IEnumerable<RetryReliability> reliabilities)
    {
        foreach (var r in reliabilities)
        {
            r.InsertionTime = DateTimeOffset.Now;
            r.ModificationTime = DateTimeOffset.Now;
        }

        using var context = _contextFactory.CreateDbContext();
        context.RetryReliability.AddRange(reliabilities);

        context.SaveChanges();
    }

    /// <summary>
    /// Updates a single <see cref="RetryReliability"/> record for a given <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The identifier to locate the <see cref="RetryReliability"/> record</param>
    /// <param name="update">The update function to change the <see cref="RetryReliability"/> record</param>
    public void UpdateRetryReliability(long id, Action<RetryReliability> update)
    {
        using var context = _contextFactory.CreateDbContext();
        var rr = context.RetryReliability.SingleOrDefault(r => r.Id == id);
        if (rr != null)
        {
            update(rr);
            rr.ModificationTime = DateTimeOffset.Now;
        }

        context.SaveChanges();
    }

    #endregion

    public SmpConfiguration? FindSmpResponseForToParty(Model.Core.Party party)
    {
        var primaryPartyType = party.PartyIds
            .FirstOrNothing()
            .SelectMany(x => x.Type)
            .GetOrElse(() => "");

        using var context = _contextFactory.CreateDbContext();
        return context.SmpConfigurations
            .FirstOrDefault(
                sc => sc.PartyRole == party.Role
                        && sc.ToPartyId == party.PrimaryPartyId
                        && sc.PartyType == primaryPartyType!)
            ?? throw new ConfigurationErrorsException("No SMP Response found for the given "
                + $"'Role': {party.Role}, 'PartyId': {party.PrimaryPartyId}, and 'PartyType': {primaryPartyType}");
    }

    public MessageEntity? GetInOrOutMessageEntityFor(string refToMessageId)
    {
        using var context = _contextFactory.CreateDbContext();

        MessageEntity? ent = context.InMessages.FirstOrDefault(m =>
            m.EbmsMessageId == refToMessageId &&
            m.EbmsMessageType == MessageType.UserMessage);

        ent ??= context.OutMessages.FirstOrDefault(m =>
            m.EbmsMessageId == refToMessageId &&
            m.EbmsMessageType == MessageType.UserMessage);

        return ent;
    }

    public OutMessage? RetrieveUserMessageForPullRequest(PullRequest pullRequest)
    {
        using var context = _contextFactory.CreateDbContext();
        context.Database.BeginTransaction(System.Data.IsolationLevel.RepeatableRead);

        var message = context.OutMessages
            .Where(PullRequestQuery(pullRequest))
            .OrderBy(m => m.InsertionTime).Take(1).FirstOrDefault();

        if (message == null)
        {
            _logger.LogWarning("No UserMessage found for PullRequest.Mpc: {Mpc}", pullRequest.Mpc);
            return null;
        }

        message.Operation = Operation.Sent;

        context.SaveChanges();
        context.Database.CommitTransaction();

        _logger.LogInformation("(PullSend) UserMessage found for PullRequest.Mpc: {Mpc}", pullRequest.Mpc);
        return message;
    }

    private Expression<Func<OutMessage, bool>> PullRequestQuery(PullRequest pullRequest)
    {
        _logger.LogDebug("Query UserMessages with MPC={Mpc} && Operation=ToBeSent && MEP=Pull", pullRequest.Mpc);

        return m => m.Mpc == pullRequest.Mpc &&
                    m.Operation == Operation.ToBeSent &&
                    m.MEP == MessageExchangePattern.Pull;
    }

    public async Task InsertJournalsAsync(IEnumerable<Journal> entries, CancellationToken cancellation)
    {
        entries = entries.Where(e => e != null);

        if (!entries.Any())
        {
            return;
        }

        using var context = _contextFactory.CreateDbContext();
        context.Journal.AddRange(entries);

        await context.SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken: cancellation);
    }
}
