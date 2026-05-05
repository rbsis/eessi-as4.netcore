using System.Net;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Receivers.Http.Get;
using Eu.EDelivery.AS4.Receivers.Http.Post;

namespace Eu.EDelivery.AS4.Receivers.Http;

public interface IRouter
{
    Task RouteWithAsync(HttpListenerContext httpContext, Func<HttpListenerRequest, Task<MessagingContext>> prePostSelector);
    IRouter Via(IHttpGetHandler handler);
    IRouter Via(IHttpPostHandler handler);
}
