using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Eu.EDelivery.AS4.Http;

internal class HttpRequest : IHttpRequest
{
    public HttpWebRequest Request { get; }

    public HttpRequest(HttpWebRequest request)
    {
        Request = request;
    }

    public void AddClientCertificates(X509Certificate value)
    {
        Request.ClientCertificates.Add(value);
    }
}
