using System.Security.Cryptography.X509Certificates;
using Eu.EDelivery.AS4.Model.PMode;
using Newtonsoft.Json.Linq;

namespace Eu.EDelivery.AS4.Fe.Pmodes.Model;

/// <summary>
/// Class to hold a sending pmode
/// </summary>
public class SendingBasePmode : BasePmode<SendingProcessingMode>
{
    private SendingProcessingMode? _pmode;

    /// <summary>
    /// Gets or sets the pmode.
    /// </summary>
    /// <value>
    /// The pmode.
    /// </value>
    public override SendingProcessingMode? Pmode
    {
        get => _pmode;
        set
        {
            _pmode = value;

            if (value?.MepBinding == MessageExchangePatternBinding.Pull)
            {
                value.PushConfiguration = null;
            }
            else if (value?.PushConfiguration?.TlsConfiguration != null
                     && value?.PushConfiguration?.TlsConfiguration?.ClientCertificateInformation is JObject clientCert)
            {
                if (clientCert["certificate"] != null)
                {
                    value.PushConfiguration.TlsConfiguration.ClientCertificateInformation =
                        clientCert.ToObject<PrivateKeyCertificate>();
                }
                else
                {
                    var jsonFindType = clientCert["clientCertificateFindType"] != null
                        ? clientCert["clientCertificateFindType"]!.Value<string>()
                        : ClientCertificateReference.DefaultCertificateFindType.ToString();

                    var jsonFindValue = clientCert["clientCertificateFindValue"] != null
                        ? clientCert["clientCertificateFindValue"]!.Value<string>()
                        : string.Empty;

                    var certFindType = Enum.TryParse(jsonFindType, out X509FindType result)
                        ? result
                        : ClientCertificateReference.DefaultCertificateFindType;

                    value.PushConfiguration.TlsConfiguration.ClientCertificateInformation = new ClientCertificateReference
                    {
                        ClientCertificateFindType = certFindType,
                        ClientCertificateFindValue = jsonFindValue
                    };
                }
            }

            if (value?.Security?.Encryption != null
                && value?.Security?.Encryption?.EncryptionCertificateInformation is JObject encryptCert)
            {
                if (encryptCert["certificate"] != null)
                {
                    value.Security.Encryption.EncryptionCertificateInformation =
                        encryptCert.ToObject<PublicKeyCertificate>();
                }
                else if (encryptCert["certificateFindType"] != null
                         && encryptCert["certificateFindValue"] != null)
                {
                    value.Security.Encryption.EncryptionCertificateInformation =
                        encryptCert.ToObject<CertificateFindCriteria>();
                }
            }

            if (value?.Security?.Signing?.SigningCertificateInformation != null
                && value.Security.Signing.SigningCertificateInformation is JObject signing)
            {
                if (signing["certificate"] != null
                    && signing["password"] != null)
                {
                    value.Security.Signing.SigningCertificateInformation =
                        signing.ToObject<PrivateKeyCertificate>();
                }
                else if (signing["certificateFindType"] != null
                    && signing["certificateFindValue"] != null)
                {
                    value.Security.Signing.SigningCertificateInformation =
                        signing.ToObject<CertificateFindCriteria>();
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether push configuration is enabled.
    /// Setting this to false will result in the PushConfiguration node being removed from the xml.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is push configuration enabled; otherwise, <c>false</c>.
    /// </value>
    public bool IsDynamicDiscoveryEnabled
    {
        get => _pmode?.PushConfiguration == null;
        set
        {
            if (_pmode != null)
            {
                if (value)
                {
                    _pmode.PushConfiguration = null;
                }
                else
                {
                    _pmode.DynamicDiscovery = null;
                }
            }
        }
    }
}
