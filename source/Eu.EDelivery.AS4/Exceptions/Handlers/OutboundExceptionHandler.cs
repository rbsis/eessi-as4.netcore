using System.Transactions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Services;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Exceptions.Handlers;

public class OutboundExceptionHandler : IAgentExceptionHandler
{
    private readonly ILogger<OutboundExceptionHandler> _logger;
    private readonly IExceptionService _exceptionService;
    private readonly ISerializerProvider _serializerProvider;

    private static readonly TransactionOptions _transactionOptions = new()
    {
        IsolationLevel = IsolationLevel.ReadCommitted,
        Timeout = TransactionManager.MaximumTimeout
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboundExceptionHandler" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="exceptionService"></param>
    /// <param name="serializerProvider"></param>
    public OutboundExceptionHandler(
        ILogger<OutboundExceptionHandler> logger,
        IExceptionService exceptionService,
        ISerializerProvider serializerProvider)
    {
        _logger = logger;
        _exceptionService = exceptionService;
        _serializerProvider = serializerProvider;
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
        _logger.LogError(exception, "Transformation exception");

        await _exceptionService.InsertOutgoingExceptionAsync(exception, messageToTransform.UnderlyingStream, cancellation);

        return new MessagingContext(exception);
    }

    /// <summary>
    /// Handles the error exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> HandleErrorExceptionAsync(Exception exception, MessagingContext context, CancellationToken cancellation)
    {
        _logger.LogError(exception, "Exception occured while executing Error Pipeline");
        return await HandleExecutionExceptionAsync(exception, context, cancellation);
    }

    /// <summary>
    /// Handles the execution exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The message context.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> HandleExecutionExceptionAsync(Exception exception, MessagingContext context, CancellationToken cancellation)
    {
        _logger.LogError(exception, "Exception occured while executing Steps");

        var ebmsMessageId = await GetEbmsMessageIdAsync(context, cancellation);

        using (var scope = new TransactionScope(TransactionScopeOption.Required, _transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
        {
            var entity = context.SubmitMessage != null
                ? await _exceptionService.InsertOutgoingSubmitExceptionAsync(exception, context.SubmitMessage, context.SendingPMode, cancellation)
                : await _exceptionService.InsertOutgoingAS4MessageExceptionAsync(exception, ebmsMessageId, context.MessageEntityId, context.SendingPMode, cancellation);

            _exceptionService.InsertRelatedRetryReliability(entity, context.SendingPMode?.ExceptionHandling?.Reliability);

            scope.Complete();
        }

        return new MessagingContext(exception);
    }

    private async Task<string?> GetEbmsMessageIdAsync(MessagingContext context, CancellationToken cancellation)
    {
        var ebmsMessageId = context.EbmsMessageId;

        if (string.IsNullOrWhiteSpace(ebmsMessageId) && context.ReceivedMessage != null)
        {
            var as4Message = await TryDeserializeAsync(context.ReceivedMessage, cancellation);
            ebmsMessageId = as4Message?.GetPrimaryMessageId();
        }

        return ebmsMessageId;
    }

    private async Task<AS4Message?> TryDeserializeAsync(ReceivedMessage message, CancellationToken cancellation)
    {
        var serializer = _serializerProvider.Get(message.ContentType);
        try
        {
            message.UnderlyingStream.Position = 0;

            return await serializer.DeserializeAsync(
                message.UnderlyingStream,
                message.ContentType,
                cancellation);
        }
        catch
        {
            return null;
        }
    }
}

