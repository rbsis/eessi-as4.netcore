using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using Eu.EDelivery.AS4.Entities;

namespace Eu.EDelivery.AS4.UnitTests.Repositories;

internal static class DatastoreExtensions
{
    /// <summary>
    /// Gets the <see cref="InMessage"/> instance for a given <paramref name="predicate"/>.
    /// </summary>
    /// <param name="createContext">The factory containing the datastore to get the record from</param>
    /// <param name="predicate">The predicate to locate the <see cref="InMessage"/> record</param>
    /// <returns></returns>
    public static InMessage? GetInMessage(this Func<DatastoreContext> createContext, Func<InMessage, bool> predicate)
    {
        return RetrieveEntity(createContext, ctx => ctx.InMessages.FirstOrDefault(predicate));
    }

    /// <summary>
    /// Gets the <see cref="InMessage"/> instances for a given <paramref name="predicate"/>.
    /// </summary>
    /// <param name="createContext">The factory containing the datastore to get the records from</param>
    /// <param name="predicate">The predicate to locate the <see cref="InMessage"/> records</param>
    /// <returns></returns>
    public static IEnumerable<InMessage> GetInMessages(this Func<DatastoreContext> createContext, Func<InMessage, bool> predicate)
    {
        return RetrieveEntity(createContext, ctx => ctx.InMessages.Where(predicate).ToArray()) ?? [];
    }

    /// <summary>
    /// Gets the <see cref="RetryReliability"/> instance for a given <paramref name="predicate"/>.
    /// </summary>
    /// <param name="createContext">The factory containing the datastore to get the record from</param>
    /// <param name="predicate">The predicate to locate the <see cref="RetryReliability"/> record</param>
    /// <returns></returns>
    public static RetryReliability? GetRetryReliability(
        this Func<DatastoreContext> createContext,
        Func<RetryReliability, bool> predicate)
    {
        return RetrieveEntity(createContext, ctx => ctx.RetryReliability.FirstOrDefault(predicate));
    }

    /// <summary>
    /// Inserts the out message.
    /// </summary>
    /// <param name="createContext">The create context.</param>
    /// <param name="message">The message.</param>
    /// <param name="withReceptionAwareness"></param>
    /// <returns>The OutMessage that has been inserted</returns>
    public static OutMessage InsertOutMessage(
        this Func<DatastoreContext> createContext,
        OutMessage message,
        bool withReceptionAwareness = false)
    {
        using var context = createContext();
        context.OutMessages.Add(message);
        context.SaveChanges();

        if (withReceptionAwareness)
        {
            context.Add(
                RetryReliability.CreateForOutMessage(
                    refToOutMessageId: message.Id,
                    maxRetryCount: 0,
                    retryInterval: TimeSpan.Zero,
                    type: RetryType.Send));

            context.SaveChanges();
        }

        return message;
    }

    /// <summary>
    /// Inserts the in message.
    /// </summary>
    /// <param name="createContext">The create context.</param>
    /// <param name="message">The message.</param>
    public static InMessage InsertInMessage(this Func<DatastoreContext> createContext, InMessage message)
    {
        using var context = createContext();
        context.InMessages.Add(message);
        context.SaveChanges();

        return message;
    }

    /// <summary>
    /// Inserts the in exception.
    /// </summary>
    /// <param name="createContext">The create context.</param>
    /// <param name="inException">The in exception.</param>
    public static InException InsertInException(this Func<DatastoreContext> createContext, InException inException)
    {
        using var context = createContext();
        context.InExceptions.Add(inException);
        context.SaveChanges();

        return inException;
    }

    /// <summary>
    /// Inserts the out exception.
    /// </summary>
    /// <param name="createContext">The create context.</param>
    /// <param name="outException">The out exception.</param>
    public static OutException InsertOutException(this Func<DatastoreContext> createContext, OutException outException)
    {
        using var context = createContext();
        context.OutExceptions.Add(outException);
        context.SaveChanges();

        return outException;
    }

    /// <summary>
    /// Inserts the retry reliability on a given datastore.
    /// </summary>
    /// <param name="createContext">The factory containing the datastore to insert the <see cref="RetryReliability"/> instance</param>
    /// <param name="r">The <see cref="RetryReliability"/> instance to insert</param>
    public static void InsertRetryReliability(this Func<DatastoreContext> createContext, RetryReliability r)
    {
        using var context = createContext();
        context.RetryReliability.Add(r);
        context.SaveChanges();
    }

    /// <summary>
    /// Asserts the in message.
    /// </summary>
    /// <param name="createContext">The create context.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="assertion">The assertion.</param>
    public static void AssertInMessage(this Func<DatastoreContext> createContext, string id, Action<InMessage?> assertion)
    {
        assertion(
            RetrieveEntity(
                createContext,
                c => c.InMessages.FirstOrDefault(m => m.EbmsMessageId.Equals(id))));
    }

    /// <summary>
    /// Asserts the in message with reference to message identifier.
    /// </summary>
    /// <param name="createContext">The create context.</param>
    /// <param name="refToMessageId">The reference to message identifier.</param>
    /// <param name="assertion">The assertion.</param>
    public static void AssertInMessageWithRefToMessageId(
        this Func<DatastoreContext> createContext,
        string refToMessageId,
        Action<InMessage?> assertion)
    {
        assertion(
            RetrieveEntity(
                createContext,
                c => c.InMessages.FirstOrDefault(m => refToMessageId.Equals(m.EbmsRefToMessageId))));
    }

    /// <summary>
    /// Asserts the out message.
    /// </summary>
    /// <param name="createContext">The create context.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="assertion">The assertion.</param>
    public static void AssertOutMessage(
        this Func<DatastoreContext> createContext,
        string id,
        Action<OutMessage?> assertion)
    {
        assertion(
            RetrieveEntity(
                createContext,
                c => c.OutMessages.FirstOrDefault(m => id.Equals(m.EbmsMessageId))));
    }

    /// <summary>
    /// Asserts the in exception.
    /// </summary>
    /// <param name="createContext">The create context.</param>
    /// <param name="assertion">The assertion.</param>
    public static void AssertInException(this Func<DatastoreContext> createContext, Action<InException?> assertion)
    {
        assertion(RetrieveEntity(createContext, c => c.InExceptions.FirstOrDefault()));
    }

    /// <summary>
    /// Asserts the in exception.
    /// </summary>
    /// <param name="createContext">The create context.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="assertion">The assertion.</param>
    public static void AssertInException(
        this Func<DatastoreContext> createContext,
        string id,
        Action<InException?> assertion)
    {
        assertion(
            RetrieveEntity(
                createContext,
                c => c.InExceptions.FirstOrDefault(e => id.Equals(e.EbmsRefToMessageId))));
    }

    /// <summary>
    /// Asserts the out exception.
    /// </summary>
    /// <param name="createContext">The create context.</param>
    /// <param name="assertion">The assertion.</param>
    public static void AssertOutException(this Func<DatastoreContext> createContext, Action<OutException?> assertion)
    {
        assertion(
            RetrieveEntity(
                createContext,
                c => c.OutExceptions.FirstOrDefault()));
    }

    /// <summary>
    /// Asserts the out exception.
    /// </summary>
    /// <param name="createContext">The create context.</param>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="assertion">The assertion.</param>
    public static void AssertOutException(this Func<DatastoreContext> createContext, string messageId, Action<OutException?> assertion)
    {
        assertion(
            RetrieveEntity(
                createContext,
                c => c.OutExceptions.FirstOrDefault(e => messageId.Equals(e.EbmsRefToMessageId))));
    }

    /// <summary>
    /// Asserts the related <see cref="RetryReliability"/> entry for a given <see cref="InMessage"/> identifier.
    /// </summary>
    /// <param name="createContext">The factory containing the datastore where to assert on</param>
    /// <param name="messageId">The message identifier to locate the related <see cref="InMessage"/></param>
    /// <param name="assertion">The assertion to run on the found <see cref="RetryReliability"/></param>
    public static void AssertRetryRelatedInMessage(
        this Func<DatastoreContext> createContext,
        long messageId,
        Action<RetryReliability?> assertion)
    {
        AssertRetryRelated(createContext, rr => rr.RefToInMessageId.HasValue && rr.RefToInMessageId.Value == messageId, assertion);
    }

    /// <summary>
    /// Asserts the related <see cref="RetryReliability"/> entry for a given <see cref="OutMessage"/> identifier.
    /// </summary>
    /// <param name="createContext">The factory containing the datastore where to assert on</param>
    /// <param name="messageId">The message identifier to locate the related <see cref="OutMessage"/></param>
    /// <param name="assertion">The assertion to run on the found <see cref="RetryReliability"/></param>
    public static void AssertRetryRelatedOutMessage(
        this Func<DatastoreContext> createContext,
        long messageId,
        Action<RetryReliability?> assertion)
    {
        AssertRetryRelated(createContext, rr => rr.RefToOutMessageId.HasValue && rr.RefToOutMessageId.Value == messageId, assertion);
    }

    /// <summary>
    /// Asserts the related <see cref="RetryReliability"/> entry for a given <see cref="InException"/> identifier.
    /// </summary>
    /// <param name="createContext">The factory containing the datastore where to assert on</param>
    /// <param name="exceptionId">The exception identifier to locate the related <see cref="InException"/></param>
    /// <param name="assertion">The assertion to run on the found <see cref="RetryReliability"/></param>
    public static void AssertRetryRelatedInException(
        this Func<DatastoreContext> createContext,
        long exceptionId,
        Action<RetryReliability?> assertion)
    {
        AssertRetryRelated(createContext, rr => rr.RefToInExceptionId.HasValue && rr.RefToInExceptionId.Value == exceptionId, assertion);
    }

    /// <summary>
    /// Asserts the related <see cref="RetryReliability"/> entry for a given <see cref="OutException"/> identifier.
    /// </summary>
    /// <param name="createContext">The factory containing the datastore where to assert on</param>
    /// <param name="exceptionId">The exception identifier to locate the related <see cref="OutException"/></param>
    /// <param name="assertion">The assertion to run on the found <see cref="RetryReliability"/></param>
    public static void AssertRetryRelatedOutException(
        this Func<DatastoreContext> createContext,
        long exceptionId,
        Action<RetryReliability?> assertion)
    {
        AssertRetryRelated(createContext, rr => rr.RefToOutExceptionId.HasValue && rr.RefToOutExceptionId.Value == exceptionId, assertion);
    }

    private static void AssertRetryRelated(
        Func<DatastoreContext> createContext,
        Expression<Func<RetryReliability, bool>> predicate,
        Action<RetryReliability?> assertion)
    {
        assertion(
            RetrieveEntity(
                createContext,
                ctx => ctx.RetryReliability.FirstOrDefault(predicate)));
    }

    private static T? RetrieveEntity<T>(Func<DatastoreContext> createContext, Func<DatastoreContext, T?> selection)
    {
        using var context = createContext();
        return selection(context);
    }
}
