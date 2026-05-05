using System.Net;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Receivers.Http.Post;

/// <summary>
/// HTTP POST handler to respond with a synchronous <see cref="SignalMessage"/>.
/// </summary>
internal class SyncSignalResponseHandler : IHttpPostHandler
{
    private readonly ILogger<SyncSignalResponseHandler> _logger;
    private readonly IHttpResultTransformer _resultTransformer;

    public SyncSignalResponseHandler(ILogger<SyncSignalResponseHandler> logger, IHttpResultTransformer resultTransformer)
    {
        _logger = logger;
        _resultTransformer = resultTransformer;
    }

    /// <summary>
    /// Determines if the resulted context can be handled by this instance.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public bool CanHandle(MessagingContext context)
    {
        return context.AS4Message != null && !context.AS4Message.IsEmpty;
    }

    /// <summary>
    /// Handles the resulted context.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public HttpResult Handle(MessagingContext context)
    {
        ArgumentNullException.ThrowIfNull(context.AS4Message);

        var statusCode = DetermineHttpCodeFrom(context);
        _logger.LogDebug("Respond with {IntStatusCode} {StatusCode}: Receipt/Errors are responded sync",
            (int)statusCode,
            statusCode);

        return _resultTransformer.FromAS4Message(statusCode, context.AS4Message);
    }

    private static HttpStatusCode DetermineHttpCodeFrom(MessagingContext agentResult)
    {
        if (agentResult.ReceivingPMode != null && agentResult.AS4Message?.PrimaryMessageUnit is Error)
        {
            var errorHttpCode = agentResult.ReceivingPMode.ReplyHandling?.ErrorHandling?.ResponseHttpCode;
            if (errorHttpCode.HasValue && Enum.IsDefined(typeof(HttpStatusCode), errorHttpCode))
            {
                return (HttpStatusCode)errorHttpCode;
            }

            return HttpStatusCode.InternalServerError;
        }

        return HttpStatusCode.OK;
    }
}
