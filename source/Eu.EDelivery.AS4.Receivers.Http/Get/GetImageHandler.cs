using System.Net;

namespace Eu.EDelivery.AS4.Receivers.Http.Get;

/// <summary>
/// HTTP GET handler to respond with the image of the component.
/// </summary>
internal class GetImageHandler : IHttpGetHandler
{
    /// <summary>
    /// Determines if the incoming request can be handled by this instance.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public bool CanHandle(HttpListenerRequest request)
    {
        return request.AcceptTypes?.Any(h => h.StartsWith("image/", StringComparison.InvariantCultureIgnoreCase)) ?? false;
    }

    /// <summary>
    /// Handle the incoming request.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public HttpResult Handle(HttpListenerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Url);
        ArgumentNullException.ThrowIfNull(request.UrlReferrer);

        var file = request.Url.ToString().Replace(request.UrlReferrer.ToString(), "./");

        if (!File.Exists(file))
        {
            return HttpResult.Empty(HttpStatusCode.NotFound);
        }

        return HttpResult.FromBytes(HttpStatusCode.OK, File.ReadAllBytes(file), "image/jpeg");
    }
}
