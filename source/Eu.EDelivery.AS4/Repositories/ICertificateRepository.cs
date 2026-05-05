using System.Security.Cryptography.X509Certificates;

namespace Eu.EDelivery.AS4.Repositories;

public interface ICertificateRepository
{
    /// <summary>
    /// Find a <see cref="X509Certificate2"/> based on the given <paramref name="privateKeyReference"/> for the <paramref name="findType"/>.
    /// </summary>
    /// <param name="findType">Kind of searching approach.</param>
    /// <param name="privateKeyReference">Value to search in the repository.</param>
    /// <returns></returns>
    X509Certificate2 GetCertificate(X509FindType findType, string privateKeyReference);
}
