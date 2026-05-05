using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.UnitTests.Exceptions.Handlers;

public class SpyAgentExceptionHandler : IAgentExceptionHandler
{
    public bool HandledTransformationException { get; private set; }
    public bool HandledExecutionException { get; private set; }
    public bool HandledErrorException { get; private set; }

    /// <summary>
    /// Handles the transformation exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="messageToTransform">The contents.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public Task<MessagingContext> HandleTransformationExceptionAsync(Exception exception, ReceivedMessage messageToTransform, CancellationToken cancellation)
    {
        HandledTransformationException = true;
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
        HandledExecutionException = true;
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
        HandledErrorException = true;
        return Task.FromResult(new MessagingContext(exception));
    }
}
