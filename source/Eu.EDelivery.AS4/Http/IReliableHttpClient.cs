using Eu.EDelivery.AS4.Http.Response;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Http;

public interface IReliableHttpClient
{
    /// <summary>
    /// Request a Message for the <see cref="IReliableHttpClient"/> implementation.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="contentType"></param>
    /// <returns></returns>
    IHttpRequest CreateRequest(string url, string contentType);

    /// <summary>
    /// Send a post request to the configured target.
    /// </summary>
    /// <param name="request">To be send <see cref="IHttpRequest"/>.</param>
    /// <param name="ctx">The <see cref="MessagingContext"/></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    Task<IAS4Response> PostRequestAsync(IHttpRequest request, MessagingContext ctx, CancellationToken cancellation);
}
