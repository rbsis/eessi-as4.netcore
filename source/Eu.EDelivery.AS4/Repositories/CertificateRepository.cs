using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Eu.EDelivery.AS4.Common;

namespace Eu.EDelivery.AS4.Repositories;

/// <summary>
/// Repository to expose the Certificate from the Certificate Store
/// </summary>
[Info("Certificate repository")]
public class CertificateRepository : ICertificateRepository
{
    private readonly IConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateRepository"/> class
    /// Create a Certificate Repository with a given Configuration
    /// </summary>
    /// <param name="config">
    /// </param>
    public CertificateRepository(IConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Get the <see cref="X509Certificate2"/>
    /// from the Certificate Store
    /// </summary>
    /// <param name="findType"></param>
    /// <param name="privateKeyReference"></param>
    /// <returns></returns>
    public X509Certificate2 GetCertificate(X509FindType findType, string privateKeyReference)
    {
        using var certificateStore = GetCertificateStore();
        certificateStore.Open(OpenFlags.ReadOnly);

        var certificateCollection =
            certificateStore.Certificates.Find(findType, privateKeyReference, validOnly: false);

        if (certificateCollection.Count <= 0)
        {
            throw new CryptographicException(
                  $"Could not find certificate in store: {_config.CertificateStore} where {findType} is {privateKeyReference}");
        }

        return certificateCollection[0];
    }

    private X509Store GetCertificateStore() => _config.CertificateStore != null
        ? new X509Store(_config.CertificateStore, StoreLocation.LocalMachine)
        : new X509Store(StoreName.My, StoreLocation.LocalMachine);
}
