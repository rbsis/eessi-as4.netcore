using System.Net;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Receivers.Http.Post;

/// <summary>
/// HTTP POST handler to return a result for responding asynchronous <see cref="SignalMessage"/> models.
/// </summary>
internal class AsyncSignalResponseHandler : IHttpPostHandler
{
    private readonly ILogger<AsyncSignalResponseHandler> _logger;

    public AsyncSignalResponseHandler(ILogger<AsyncSignalResponseHandler> logger)
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
        return context.ReceivingPMode != null
               && context.ReceivingPMode.ReplyHandling?.ReplyPattern == ReplyPattern.Callback;
    }

    /// <summary>
    /// Handles the resulted context.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public HttpResult Handle(MessagingContext context)
    {
        _logger.LogDebug("Respond with 202 Accepted: Receipt/Errors are responded async");
        return HttpResult.Empty(HttpStatusCode.Accepted);
    }
}
