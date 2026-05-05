using System.Net;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Http.Response;

internal interface IAS4ResponseFactory
{
    Task<IAS4Response> Create(MessagingContext requestMessage, HttpWebResponse webResponse, CancellationToken cancellation);
    Task<IAS4Response> CreateAsync(MessagingContext requestMessage, HttpResponseMessage responseMessage, CancellationToken cancellation);
}
