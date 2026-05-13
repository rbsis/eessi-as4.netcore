using System.Net;
using Eu.EDelivery.AS4.Model.Internal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Receivers.Http.Post;

internal class AcceptedResponseHandler : IHttpPostHandler
{
    private readonly ILogger<AcceptedResponseHandler> _logger;

    public AcceptedResponseHandler(ILogger<AcceptedResponseHandler> logger)
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
        return context.Exception == null
               && context.ErrorResult == null
               && context.AS4Message != null
               && context.AS4Message.IsEmpty;
    }

    /// <summary>
    /// Handles the resulted context.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public HttpResult Handle(MessagingContext context)
    {
        _logger.LogDebug("Respond with 202 Accepted: unknown reason");
        return HttpResult.Empty(HttpStatusCode.Accepted);
    }
}
