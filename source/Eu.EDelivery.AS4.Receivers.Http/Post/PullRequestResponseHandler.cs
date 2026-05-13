using System.Net;
using Eu.EDelivery.AS4.Model.Internal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Receivers.Http.Post;

/// <summary>
/// HTTP POST handler to respond to a <see cref="Model.Core.PullRequest"/>.
/// </summary>
internal class PullRequestResponseHandler : IHttpPostHandler
{
    private readonly ILogger<PullRequestResponseHandler> _logger;

    public PullRequestResponseHandler(ILogger<PullRequestResponseHandler> logger)
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
        return context.Mode == MessagingContextMode.Send && context.ReceivedMessage != null;
    }

    /// <summary>
    /// Handles the resulted context.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public HttpResult Handle(MessagingContext context)
    {
        ArgumentNullException.ThrowIfNull(context.ReceivedMessage);

        _logger.LogDebug("Respond with 200 OK: AS4Message is result of pulling");

        // When we're sending as a puller, make sure that the message that has been received, 
        // is directly written to the stream.
        return HttpResult.FromStream(
            HttpStatusCode.OK,
            context.ReceivedMessage.UnderlyingStream,
            context.ReceivedMessage.ContentType);
    }
}
