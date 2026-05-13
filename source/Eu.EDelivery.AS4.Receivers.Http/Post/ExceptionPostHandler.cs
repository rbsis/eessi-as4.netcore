using System.Net;
using System.Security;
using System.Text;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Internal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Receivers.Http.Post;

/// <summary>
/// HTTP POST handler to correctly return a response for both unhandled Errors and Exceptions.
/// </summary>
internal class ExceptionPostHandler : IHttpPostHandler
{
    private readonly ILogger<ExceptionPostHandler> _logger;

    public ExceptionPostHandler(ILogger<ExceptionPostHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Determines if the resulted context can be handled by this instance.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    /// 
    public bool CanHandle(MessagingContext context)
    {
        return
            context.Exception != null
            || context.ErrorResult != null
            && (context.AS4Message?.IsEmpty ?? true);
    }

    /// <summary>
    /// Handles the resulted context.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public HttpResult Handle(MessagingContext context)
    {
        var statusCode = DetermineStatusCode(context.Exception);
        const string ErrorMessage = "something went wrong while processing the request";

        _logger.LogError(context.Exception, "Respond with {IntStatusCode} {StatusCode} {ErrorMessage}",
            (int)statusCode,
            statusCode,
            ErrorMessage);
        return HttpResult.FromBytes(
            statusCode,
            Encoding.UTF8.GetBytes(ErrorMessage),
            "text/plain");
    }

    private static HttpStatusCode DetermineStatusCode(Exception? exception) => exception switch
    {
        SecurityException _ => HttpStatusCode.Forbidden,
        InvalidMessageException _ => HttpStatusCode.BadRequest,
        _ => HttpStatusCode.InternalServerError,
    };
}
