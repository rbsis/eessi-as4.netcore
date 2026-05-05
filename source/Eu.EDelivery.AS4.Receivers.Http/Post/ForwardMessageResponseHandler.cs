using System.Net;
using Eu.EDelivery.AS4.Model.Internal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Receivers.Http.Post;

/// <summary>
/// HTTP POST handler to return a response when the request must be forwarded.
/// </summary>
internal class ForwardMessageResponseHandler : IHttpPostHandler
{
    private readonly ILogger<ForwardMessageResponseHandler> _logger;

    public ForwardMessageResponseHandler(ILogger<ForwardMessageResponseHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Determines if the resulted context can be handled by this instance.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public bool CanHandle(MessagingContext context)
    {
        return context.Mode == MessagingContextMode.Receive && context.ReceivedMessageMustBeForwarded;
    }

    /// <summary>
    /// Handles the resulted context.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public HttpResult Handle(MessagingContext context)
    {
        _logger.LogDebug("Respond with 202 Accepted: message will be forwarded");
        return HttpResult.Empty(HttpStatusCode.Accepted);
    }
}
