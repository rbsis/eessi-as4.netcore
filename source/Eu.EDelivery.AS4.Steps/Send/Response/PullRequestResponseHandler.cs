using System.Net;
using Eu.EDelivery.AS4.Http.Response;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Services;
using Eu.EDelivery.AS4.Strategies.Sender;
using Microsoft.Extensions.DependencyInjection;

namespace Eu.EDelivery.AS4.Steps.Send.Response;

/// <summary>
/// <see cref="IAS4ResponseHandler"/> implementation to handle the response for a Pull Request.
/// </summary>
internal sealed class PullRequestResponseHandler : IAS4ResponseHandler
{
    private readonly IAS4ResponseHandler _nextHandler;
    private readonly IPiggyBackingService _piggyBackingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestResponseHandler"/> class.
    /// </summary>
    public PullRequestResponseHandler(
        [FromKeyedServices(typeof(EmptyBodyResponseHandler))] IAS4ResponseHandler nextHandler,
        IPiggyBackingService piggyBackingService)
    {
        _nextHandler = nextHandler;
        _piggyBackingService = piggyBackingService;
    }

    /// <summary>
    /// Handle the given <paramref name="response" />, but delegate to the next handler if you can't.
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> HandleResponseAsync(IAS4Response response, CancellationToken cancellation)
    {
        var request = response.OriginalRequest;
        if (request?.AS4Message?.IsPullRequest == true)
        {
            var pullRequestWasPiggyBacked =
                request.AS4Message.SignalMessages.Any(s => s is not PullRequest);

            if (pullRequestWasPiggyBacked)
            {
                var result = response.StatusCode == HttpStatusCode.Accepted || response.StatusCode == HttpStatusCode.OK
                    ? SendResult.Success
                    : SendResult.RetryableFail;

                _piggyBackingService.ResetSignalMessagesToBePiggyBacked(request.AS4Message.SignalMessages, result);
            }

            var isEmptyChannelWarning = response.ReceivedAS4Message?.FirstSignalMessage is Error { IsPullRequestWarning: true };
            if (isEmptyChannelWarning)
            {
                request.ModifyContext(response.ReceivedAS4Message!, MessagingContextMode.Send);
                return (await StepResult.SuccessAsync(response.OriginalRequest)).AndStopExecution();
            }
        }

        return await _nextHandler.HandleResponseAsync(response, cancellation);
    }
}
