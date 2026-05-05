using System.Transactions;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Services;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Exceptions.Handlers;

internal class InboundExceptionHandler : IAgentExceptionHandler
{
    private readonly ILogger<InboundExceptionHandler> _logger;
    private readonly IExceptionService _exceptionService;

    private static readonly TransactionOptions _transactionOptions = new()
    {
        IsolationLevel = IsolationLevel.ReadCommitted,
        Timeout = TransactionManager.MaximumTimeout
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundExceptionHandler"/> class.
    /// </summary>
    public InboundExceptionHandler(
        ILogger<InboundExceptionHandler> logger,
        IExceptionService exceptionService)
    {
        _logger = logger;
        _exceptionService = exceptionService;
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

        await _exceptionService.InsertIncomingExceptionAsync(exception, messageToTransform.UnderlyingStream, cancellation);

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
        _logger.LogError(exception, "Error exception");

        return await HandleExecutionExceptionAsync(exception, context, cancellation);
    }

    /// <summary>
    /// Handles the execution exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> HandleExecutionExceptionAsync(Exception exception, MessagingContext context, CancellationToken cancellation)
    {
        _logger.LogError(exception, "Execution exception");

        using (var scope = new TransactionScope(TransactionScopeOption.Required, _transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
        {
            var entity = context.SubmitMessage != null
                ? await _exceptionService.InsertIncomingSubmitExceptionAsync(exception, context.SubmitMessage, context.ReceivingPMode, cancellation)
                : await _exceptionService.InsertIncomingAS4MessageExceptionAsync(exception, context.EbmsMessageId, context.ReceivingPMode, cancellation);


            _exceptionService.InsertRelatedRetryReliability(entity, context.ReceivingPMode?.ExceptionHandling?.Reliability);

            scope.Complete();
        }

        return new MessagingContext(exception)
        {
            ErrorResult = context.ErrorResult
        };
    }
}
