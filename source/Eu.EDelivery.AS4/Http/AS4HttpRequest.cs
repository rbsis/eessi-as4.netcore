using System.Security.Cryptography.X509Certificates;

namespace Eu.EDelivery.AS4.Http;

internal class AS4HttpRequest : IHttpRequest
{
    public string Url { get; }
    public string ContentType { get; }
    public X509Certificate? Certificate { get; private set; }

    public AS4HttpRequest(string url, string contentType)
    {
        Url = url;
        ContentType = contentType;
    }

    public void AddClientCertificates(X509Certificate value)
    {
        Certificate = value;
    }
}
