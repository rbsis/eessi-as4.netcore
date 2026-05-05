using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Exceptions.Handlers;

internal class PullSendAgentExceptionHandler : IAgentExceptionHandler
{
    private readonly IAgentExceptionHandler _outboundExceptionHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="PullSendAgentExceptionHandler" /> class.
    /// </summary>
    /// <param name="outboundHandler"></param>
    public PullSendAgentExceptionHandler(OutboundExceptionHandler outboundHandler)
    {
        _outboundExceptionHandler = outboundHandler;
    }

    /// <summary>
    /// Handles the transformation exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="messageToTransform">The <see cref="ReceivedMessage"/> that must be transformed by the transformer.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> HandleTransformationExceptionAsync(Exception exception, ReceivedMessage messageToTransform, CancellationToken cancellation) =>
        await _outboundExceptionHandler.HandleTransformationExceptionAsync(exception, messageToTransform, cancellation);

    /// <summary>
    /// Handles the execution exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> HandleExecutionExceptionAsync(Exception exception, MessagingContext context, CancellationToken cancellation) =>
        await _outboundExceptionHandler.HandleExecutionExceptionAsync(exception, context, cancellation);

    /// <summary>
    /// Handles the error exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> HandleErrorExceptionAsync(Exception exception, MessagingContext context, CancellationToken cancellation) =>
        await _outboundExceptionHandler.HandleErrorExceptionAsync(exception, context, cancellation);

}
