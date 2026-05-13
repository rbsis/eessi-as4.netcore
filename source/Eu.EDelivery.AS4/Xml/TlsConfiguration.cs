using System.Xml.Serialization;
using Eu.EDelivery.AS4.Model.PMode;

namespace Eu.EDelivery.AS4.Xml;

/// <summary>
/// Adding Tls Version to the TlsConfiguration
/// </summary>
public partial class TlsConfiguration
{
    [Info("Tls version", defaultValue: TlsVersion.Tls12)]
    private TlsVersion _tlsVersion = TlsVersion.Tls12;

    [XmlElement("TlsVersion")]
    public string TlsVersionString
    {
        get { return _tlsVersion.ToString(); }
        set { _tlsVersion = (TlsVersion)Enum.Parse(typeof(TlsVersion), value); }
    }

    [XmlIgnore]
    public TlsVersion TlsVersion
    {
        get { return _tlsVersion; }
        set { _tlsVersion = value; }
    }
}
