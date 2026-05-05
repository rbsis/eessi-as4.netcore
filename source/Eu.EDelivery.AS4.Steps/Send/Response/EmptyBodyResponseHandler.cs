using System.Net;
using Eu.EDelivery.AS4.Http.Response;
using Eu.EDelivery.AS4.Model.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Send.Response;

/// <summary>
/// <see cref="IAS4ResponseHandler"/> implementation to handle the response for a empty body.
/// </summary>
internal sealed class EmptyBodyResponseHandler : IAS4ResponseHandler
{
    private readonly ILogger<EmptyBodyResponseHandler> _logger;
    private readonly IAS4ResponseHandler _nextHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmptyBodyResponseHandler"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="nextHandler">The next Handler.</param>
    public EmptyBodyResponseHandler(
        ILogger<EmptyBodyResponseHandler> logger,
        [FromKeyedServices(typeof(TailResponseHandler))] IAS4ResponseHandler nextHandler)
    {
        _nextHandler = nextHandler;
        _logger = logger;
    }

    /// <summary>
    /// Handle the given <paramref name="response" />, but delegate to the next handler if you can't.
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> HandleResponseAsync(IAS4Response response, CancellationToken cancellation)
    {
        if (response.ReceivedAS4Message.IsEmpty)
        {
            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                response.OriginalRequest.ModifyContext(response.ReceivedAS4Message, MessagingContextMode.Send);
                return (await StepResult.SuccessAsync(response.OriginalRequest)).AndStopExecution();
            }

            _logger.LogError("Response with HTTP status: {StatusCode}", response.StatusCode);

            if (_logger.IsEnabled(LogLevel.Error))
            {
                using var r = new StreamReader(response.ReceivedStream.UnderlyingStream);
                var content = await r.ReadToEndAsync(cancellation);
                if (!string.IsNullOrEmpty(content))
                {
                    _logger.LogError("Response with HTTP content: {Content}", content);
                }
            }

            response.OriginalRequest.ModifyContext(response.ReceivedStream, MessagingContextMode.Send);
            return (await StepResult.FailedAsync(response.OriginalRequest)).AndStopExecution();
        }

        return await _nextHandler.HandleResponseAsync(response, cancellation);
    }
}
