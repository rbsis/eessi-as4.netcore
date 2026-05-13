using Eu.EDelivery.AS4.Model.Internal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Exceptions.Handlers;

internal class LogExceptionHandler : IAgentExceptionHandler
{
    private readonly ILogger<LogExceptionHandler> _logger;

    public LogExceptionHandler(ILogger<LogExceptionHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles the transformation exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="messageToTransform">The <see cref="ReceivedMessage"/> that must be transformed by the transformer.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public Task<MessagingContext> HandleTransformationExceptionAsync(Exception exception, ReceivedMessage messageToTransform, CancellationToken cancellation)
    {
        _logger.LogError(exception, "Transformation exception");
        return Task.FromResult(new MessagingContext(exception));
    }

    /// <summary>
    /// Handles the execution exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public Task<MessagingContext> HandleExecutionExceptionAsync(Exception exception, MessagingContext context, CancellationToken cancellation)
    {
        _logger.LogError(exception, "Execution exception");
        return Task.FromResult(new MessagingContext(exception));
    }

    /// <summary>
    /// Handles the error exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public Task<MessagingContext> HandleErrorExceptionAsync(Exception exception, MessagingContext context, CancellationToken cancellation)
    {
        _logger.LogError(exception, "Error exception");
        return Task.FromResult(new MessagingContext(exception));
    }
}
