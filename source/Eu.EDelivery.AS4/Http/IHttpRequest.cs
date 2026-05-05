using System.Security.Cryptography.X509Certificates;

namespace Eu.EDelivery.AS4.Http;

public interface IHttpRequest
{
    void AddClientCertificates(X509Certificate value);
}
