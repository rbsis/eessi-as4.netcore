using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Repositories;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Exceptions.Handlers;

internal class NotifyExceptionHandler : IAgentExceptionHandler
{
    private readonly ILogger<NotifyExceptionHandler> _logger;
    private readonly IDatastoreRepository _repository;
    private readonly IAgentExceptionHandler _inboudHandler;
    private readonly IAgentExceptionHandler _outboundHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifyExceptionHandler"/> class.
    /// </summary>
    public NotifyExceptionHandler(
        ILogger<NotifyExceptionHandler> logger,
        InboundExceptionHandler inboudHandler,
        OutboundExceptionHandler outboundHandler,
        IDatastoreRepository repository)
    {

        _logger = logger;
        _repository = repository;
        _inboudHandler = inboudHandler;
        _outboundHandler = outboundHandler;
    }

    /// <summary>
    /// Handles the transformation exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="messageToTransform">The <see cref="ReceivedMessage"/> that must be transformed by the transformer.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> HandleTransformationExceptionAsync(Exception exception, ReceivedMessage messageToTransform, CancellationToken cancellation)
    {
        var entity = GetReceivedEntity(messageToTransform);

        if (entity is InMessage || entity is InException)
        {
            return await _inboudHandler.HandleTransformationExceptionAsync(exception, messageToTransform, cancellation);
        }
        else
        {
            return await _outboundHandler.HandleTransformationExceptionAsync(exception, messageToTransform, cancellation);
        }
    }

    private static Entity GetReceivedEntity(ReceivedMessage message)
    {
        var receivedEntityMessage = message as ReceivedEntityMessage ?? throw new InvalidOperationException($"A ReceivedEntityMessage is expected in the NotifyExceptionHandler.HandleTransformationException method instead of a {message.GetType().FullName}");
        return receivedEntityMessage.Entity;
    }

    /// <summary>
    /// Handles the execution exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> HandleExecutionExceptionAsync(Exception exception, MessagingContext context, CancellationToken cancellation) =>
        await HandleNotifyExceptionAsync(exception, context);

    /// <summary>
    /// Handles the error exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> HandleErrorExceptionAsync(Exception exception, MessagingContext context, CancellationToken cancellation) =>
        await HandleNotifyExceptionAsync(exception, context);

    private Task<MessagingContext> HandleNotifyExceptionAsync(Exception exception, MessagingContext context)
    {
        _logger.LogError(exception, "Notify exception");

        if (context.NotifyMessage?.EntityType == typeof(InMessage) || context.NotifyMessage?.EntityType == typeof(InException))
        {
            var inException = InException.ForEbmsMessageId(context.EbmsMessageId, exception);
            _repository.InsertInException(inException);

            if (context.NotifyMessage.EntityType == typeof(InMessage))
            {
                _logger.LogDebug("Fatal fail in notification, set InMessage(s).Status=Exception");
                _repository.UpdateInMessage(context.EbmsMessageId, i => i.SetStatus(InStatus.Exception));
            }
        }
        else if (context.NotifyMessage?.EntityType != typeof(OutMessage) || context.NotifyMessage?.EntityType == typeof(OutException))
        {
            var outException = OutException.ForEbmsMessageId(context.EbmsMessageId, exception);
            _repository.InsertOutException(outException);

            if (context.NotifyMessage?.EntityType == typeof(OutMessage) && context.MessageEntityId != null)
            {
                _logger.LogDebug("Fatal fail in notification, set OutMessage.Status=Exception");
                _repository.UpdateOutMessage(
                    context.MessageEntityId.Value,
                    o => o.SetStatus(OutStatus.Exception));
            }
        }

        if (context.MessageEntityId != null)
        {
            _logger.LogDebug("Abort retry operation due to fatal notification exception, set Status=Completed");
            _repository.UpdateRetryReliability(
                context.MessageEntityId.Value,
                r => r.Status = RetryStatus.Completed);
        }

        return Task.FromResult(new MessagingContext(exception));
    }
}
