using Eu.EDelivery.AS4.Model.Internal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Exceptions.Handlers;

/// <summary>
/// Wrapper for the <see cref="IAgentExceptionHandler"/> implementation to safeguard the exception handling.
/// </summary>
/// <seealso cref="IAgentExceptionHandler" />
internal class SafeExceptionHandler : IAgentExceptionHandler
{
    private readonly ILogger<SafeExceptionHandler> _logger;
    private readonly IAgentExceptionHandler _innerHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeExceptionHandler" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="handler">The handler.</param>
    public SafeExceptionHandler(ILogger<SafeExceptionHandler> logger, IAgentExceptionHandler handler)
    {
        _logger = logger;
        _innerHandler = handler;
    }

    /// <summary>
    /// Handles the transformation exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="messageToTransform">The <see cref="ReceivedMessage"/> that must be transformed by the transformer.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> HandleTransformationExceptionAsync(Exception exception, ReceivedMessage messageToTransform, CancellationToken cancellation) =>
        await TryHandling(() => _innerHandler.HandleTransformationExceptionAsync(exception, messageToTransform, cancellation));

    /// <summary>
    /// Handles the execution exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> HandleExecutionExceptionAsync(Exception exception, MessagingContext context, CancellationToken cancellation) =>
        await TryHandling(() => _innerHandler.HandleExecutionExceptionAsync(exception, context, cancellation), faultingContext: context);

    /// <summary>
    /// Handles the error exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> HandleErrorExceptionAsync(Exception exception, MessagingContext context, CancellationToken cancellation) =>
        await TryHandling(() => _innerHandler.HandleErrorExceptionAsync(exception, context, cancellation), faultingContext: context);

    private async Task<MessagingContext> TryHandling(Func<Task<MessagingContext>> actionToTry, MessagingContext faultingContext)
    {
        try
        {
            return await actionToTry();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while trying to log an error");

            if (faultingContext != null && faultingContext.Exception == null)
            {
                faultingContext.Exception = ex;
            }

            return faultingContext ?? new MessagingContext(ex);
        }
    }

    private async Task<MessagingContext> TryHandling(Func<Task<MessagingContext>> actionToTry)
    {
        try
        {
            return await actionToTry();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An error occured while trying to handle a transformation exception");
            return new MessagingContext(exception);
        }
    }
}
