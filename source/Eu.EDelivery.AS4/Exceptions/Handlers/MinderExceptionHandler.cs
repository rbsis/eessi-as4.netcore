using System.Diagnostics.CodeAnalysis;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Exceptions.Handlers;

[ExcludeFromCodeCoverage]
internal class MinderExceptionHandler : IAgentExceptionHandler
{
    private readonly IAgentExceptionHandler _inboudHandler;
    private readonly IAgentExceptionHandler _outboundHandler;

    public MinderExceptionHandler(
        InboundExceptionHandler inboudHandler,
        OutboundExceptionHandler outboundHandler)
    {
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
    public Task<MessagingContext> HandleTransformationExceptionAsync(Exception exception, ReceivedMessage messageToTransform, CancellationToken cancellation) =>
        Task.FromResult(new MessagingContext(exception));

    /// <summary>
    /// Handles the execution exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> HandleExecutionExceptionAsync(Exception exception, MessagingContext context, CancellationToken cancellation) =>
        await HandleMinderException(context, handler => handler.HandleExecutionExceptionAsync(exception, context, cancellation));

    /// <summary>
    /// Handles the error exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> HandleErrorExceptionAsync(Exception exception, MessagingContext context, CancellationToken cancellation) =>
        await HandleMinderException(context, handler => handler.HandleErrorExceptionAsync(exception, context, cancellation));

    private async Task<MessagingContext> HandleMinderException(MessagingContext context, Func<IAgentExceptionHandler, Task<MessagingContext>> handleException)
    {
        return context.Mode == MessagingContextMode.Submit
            ? await handleException(_outboundHandler)
            : await handleException(_inboudHandler);
    }
}
