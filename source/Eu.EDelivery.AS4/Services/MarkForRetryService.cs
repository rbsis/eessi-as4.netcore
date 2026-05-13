using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Strategies.Sender;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Services;

/// <summary>
/// Service abstraction to set the referenced deliver message to the right Status/Operation accordingly to the <see cref="SendResult"/>.
/// </summary>
internal class MarkForRetryService : IMarkForRetryService
{
    private readonly ILogger<MarkForRetryService> _logger;

    private readonly IDatastoreRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkForRetryService"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="repository">The repository.</param>
    public MarkForRetryService(ILogger<MarkForRetryService> logger, IDatastoreRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    /// <summary>
    /// Updates the AS4Message's Status/Operation accordingly to the status of the 
    /// </summary>
    /// <param name="messageId"></param>
    /// <param name="status"></param>
    public void UpdateAS4MessageForSendResult(long messageId, SendResult status)
    {
        _repository.UpdateOutMessage(
            outMessageId: messageId,
            updateAction: entity => UpdateMessageEntity(
                resultOfOperation: status,
                entityToBeRetried: entity,
                getRetryEntries: () => _repository.GetRetryReliability(r => r.RefToOutMessageId == entity.Id, r => r),
                onCompleted: e =>
                {
                    _logger.LogTrace("Update OutMessage with Status and Operation set to Sent");

                    e.SetStatus(OutStatus.Sent);
                    e.Operation = Operation.Sent;
                },
                onDeadLettered: e =>
                {
                    _logger.LogDebug($"AS4Message failed during the sending, exhausted retries");
                    e.SetStatus(OutStatus.Exception);
                }));
    }

    /// <summary>
    /// Updates the DeliverMessage's Status/Operation accordingly to <see cref="SendResult"/>.
    /// </summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="status">The upload status during the delivery of the payloads.</param>
    public void UpdateDeliverMessageForUploadResult(string messageId, SendResult status)
    {
        ArgumentNullException.ThrowIfNull(messageId);

        _repository.UpdateInMessage(
            messageId: messageId,
            updateAction: entity => UpdateMessageEntity(
                resultOfOperation: status,
                entityToBeRetried: entity,
                getRetryEntries: () => _repository.GetRetryReliability(r => r.RefToInMessageId == entity.Id, r => r),
                onCompleted: _ => _logger.LogTrace("Attachments are uploaded successfully, no retry is needed"),
                onDeadLettered: e =>
                {
                    _logger.LogDebug("DeliverMessage failed during the delivery, exhausted retries");
                    e.SetStatus(InStatus.Exception);
                }));
    }

    /// <summary>
    /// Updates the DeliverMessage's Status/Operation accordingly to <see cref="Eu.EDelivery.AS4.Strategies.Sender.SendResult"/>.
    /// </summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="status">The deliver status during the delivery of the deliver message.</param>
    /// <returns></returns>
    public void UpdateDeliverMessageForDeliverResult(string messageId, SendResult status)
    {
        _repository.UpdateInMessage(
            messageId: messageId,
            updateAction: entity => UpdateMessageEntity(
                resultOfOperation: status,
                entityToBeRetried: entity,
                getRetryEntries: () => _repository.GetRetryReliability(r => r.RefToInMessageId == entity.Id, r => r),
                onCompleted: e =>
                {
                    _logger.LogDebug("Update InMessage with Status and Operation set to Delivered");

                    e.SetStatus(InStatus.Delivered);
                    e.Operation = Operation.Delivered;
                },
                onDeadLettered: e =>
                {
                    _logger.LogDebug("DeliverMessage failed during the delivery, exhausted retries");
                    e.SetStatus(InStatus.Exception);
                }));
    }

    /// <summary>
    /// Updates the NotifyMessage stored as <see cref="InMessage"/> in the datastore, accordingly to the given notification result.
    /// </summary>
    /// <param name="messageId">Identifier to update the <see cref="InMessage"/></param>
    /// <param name="result">Notification result used to determine the right update values for the to be updated entity</param>
    public void UpdateNotifyMessageForIncomingMessage(long messageId, SendResult result)
    {
        _repository.UpdateInMessage(
            id: messageId,
            update: entity => UpdateMessageEntity(
                resultOfOperation: result,
                entityToBeRetried: entity,
                getRetryEntries: () => _repository.GetRetryReliability(r => r.RefToInMessageId == entity.Id, r => r),
                onCompleted: e =>
                {
                    _logger.LogDebug("Update InMessage with Status and Operation set to Notified");

                    e.SetStatus(InStatus.Notified);
                    e.Operation = Operation.Notified;
                },
                onDeadLettered: m =>
                {
                    _logger.LogDebug("NotifyMessage failed during the notification, exhausted retries");
                    m.SetStatus(InStatus.Exception);
                }));
    }

    /// <summary>
    /// Updates the NotifyMessage stored as <see cref="OutMessage"/> in the datastore, accordingly to the given notification result.
    /// </summary>
    /// <param name="messageId">Identifier to update the <see cref="OutMessage"/></param>
    /// <param name="result">Notification result used to determine the right update values for the to be updated entity</param>
    public void UpdateNotifyMessageForOutgoingMessage(long messageId, SendResult result)
    {
        _repository.UpdateOutMessage(
            messageId,
            entity => UpdateMessageEntity(
                resultOfOperation: result,
                entityToBeRetried: entity,
                getRetryEntries: () => _repository.GetRetryReliability(r => r.RefToOutMessageId == entity.Id, r => r),
                onCompleted: m =>
                {
                    _logger.LogDebug("Update InMessage with Status and Operation set to Notified");

                    m.SetStatus(OutStatus.Notified);
                    m.Operation = Operation.Notified;
                },
                onDeadLettered: m =>
                {
                    _logger.LogDebug("NotifyMessage failed during the notification, exhausted retries");
                    m.SetStatus(OutStatus.Exception);
                }));
    }

    private void UpdateMessageEntity<T>(
        SendResult resultOfOperation,
        T entityToBeRetried,
        Func<IEnumerable<RetryReliability>> getRetryEntries,
        Action<T> onCompleted,
        Action<T> onDeadLettered) where T : MessageEntity
    {
        // Only for records that are not yet been completly Notified/DeadLettered should we botter to retry
        if (entityToBeRetried.Operation == Operation.Notified
            || entityToBeRetried.Operation == Operation.DeadLettered)
        {
            return;
        }

        var rr = getRetryEntries().FirstOrDefault();
        if (resultOfOperation == SendResult.Success)
        {
            onCompleted(entityToBeRetried);

            _logger.LogTrace("Successful result, so update RetryReliability.Status=Completed");

            if (rr != null)
            {
                _repository.UpdateRetryReliability(rr.Id, rr => rr.Status = RetryStatus.Completed);
            }
        }
        else
        {
            if (rr == null)
            {
                _logger.LogDebug("Message can't be retried because no RetryReliability is configured");
                _logger.LogDebug("Update {Name} with {{Status=Exception, Operation=DeadLettered}}", nameof(T));

                onDeadLettered(entityToBeRetried);
                entityToBeRetried.Operation = Operation.DeadLettered;
            }
            else if (resultOfOperation == SendResult.RetryableFail)
            {
                _logger.LogDebug("Message failed this time, set for retry by updating {Name}.Operation=ToBeRetried", nameof(T));

                entityToBeRetried.Operation = Operation.ToBeRetried;

                _repository.UpdateRetryReliability(rr.Id, rr => rr.Status = RetryStatus.Pending);
            }
            else
            {
                _logger.LogDebug("Message failed this time due to a fatal result during sending");
                _logger.LogDebug("Update {Name} with Status=Exception, Operation=DeadLettered", nameof(T));

                onDeadLettered(entityToBeRetried);
                entityToBeRetried.Operation = Operation.DeadLettered;

                _repository.UpdateRetryReliability(rr.Id, rr => rr.Status = RetryStatus.Completed);
            }
        }
    }

    /// <summary>
    /// Updates the NotifyMessage stored as <see cref="InException"/> in the datastore, accordingly to the given notification result.
    /// </summary>
    /// <param name="messageId">Identifier to update the <see cref="InException"/></param>
    /// <param name="result">Notification result used to determine the right update values for the to be updated entity</param>
    public void UpdateNotifyExceptionForIncomingMessage(long messageId, SendResult result)
    {
        _repository.UpdateInException(
            id: messageId,
            update: exEntity => UpdateExceptionRetry(
                resultOfOperation: result,
                entityToBeRetried: exEntity,
                getRetryEntries: () => _repository.GetRetryReliability(r => r.RefToInExceptionId == exEntity.Id, r => r)));
    }

    /// <summary>
    /// Updates the NotifyMessage stored as <see cref="OutException"/> in the datastore, accordingly to the given notification result.
    /// </summary>
    /// <param name="messageId">Identifier to update the <see cref="OutException"/></param>
    /// <param name="result">Notification result used to determine the right update values for the to be updated entity</param>
    public void UpdateNotifyExceptionForOutgoingMessage(long messageId, SendResult result)
    {
        _repository.UpdateOutException(
            id: messageId,
            update: exEntity => UpdateExceptionRetry(
                resultOfOperation: result,
                entityToBeRetried: exEntity,
                getRetryEntries: () => _repository.GetRetryReliability(r => r.RefToOutExceptionId == exEntity.Id, r => r)));
    }

    private void UpdateExceptionRetry<T>(
        SendResult resultOfOperation,
        T entityToBeRetried,
        Func<IEnumerable<RetryReliability>> getRetryEntries) where T : ExceptionEntity
    {
        // There could be more In/Out Exceptions for a single message, 
        // therefore we should only look for exceptions that are not yet been Notified/DeadLettered.
        if (entityToBeRetried.Operation == Operation.Notified
            || entityToBeRetried.Operation == Operation.DeadLettered)
        {
            return;
        }

        var rr = getRetryEntries().FirstOrDefault();
        var reftoMessageId =
            entityToBeRetried.EbmsRefToMessageId == null
            ? string.Empty
            : $"[{entityToBeRetried.EbmsRefToMessageId}]";

        if (resultOfOperation == SendResult.Success)
        {
            _logger.LogDebug("Update {Name} {ReftoMessageId} with Operation=Notified", nameof(T), reftoMessageId);
            entityToBeRetried.Operation = Operation.Notified;

            if (rr != null)
            {
                rr.Status = RetryStatus.Completed;
            }
        }
        else
        {
            if (rr == null)
            {
                _logger.LogDebug("{Name} NotifyMessage failed during the notification, exhausted retries", nameof(T));
                _logger.LogDebug("Update {Name} with {{Status=Exception, Operation=DeadLettered}}", nameof(T));

                entityToBeRetried.Operation = Operation.DeadLettered;
            }
            else if (resultOfOperation == SendResult.RetryableFail)
            {
                _logger.LogDebug("{Name} NotifyMessage failed this time, will be retried by updating Operation=ToBeRetried", nameof(T));

                entityToBeRetried.Operation = Operation.ToBeRetried;
                rr.Status = RetryStatus.Pending;
            }
            else
            {
                _logger.LogDebug("{Name} NotifyMessage failed during the notification, exhausted retries", nameof(T));
                _logger.LogDebug("Update {Name} with {{Status=Exception, Operation=DeadLettered}}", nameof(T));

                entityToBeRetried.Operation = Operation.DeadLettered;
                rr.Status = RetryStatus.Completed;
            }
        }
    }
}
