using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Eu.EDelivery.AS4.Repositories;

namespace Eu.EDelivery.AS4.Security.References;

/// <summary>
/// Issuer Security Strategy to add a Security Reference to the Message
/// </summary>
internal sealed class IssuerSecurityTokenReference : SecurityTokenReference
{
    private readonly ICertificateRepository? _certificateRepository;
    private readonly string _securityTokenReferenceId;

    private string? _certificateSerialNr;

    /// <summary>
    /// Initializes a new instance of the <see cref="IssuerSecurityTokenReference" /> class.
    /// to handle <see cref="X509ReferenceType.IssuerSerial" /> configuration
    /// </summary>
    /// <param name="certificate">The certificate for which a SecurityTokenReference needs to be created.</param>
    public IssuerSecurityTokenReference(X509Certificate2 certificate)
    {
        _securityTokenReferenceId = $"STR-{Guid.NewGuid()}";
        Certificate = certificate;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IssuerSecurityTokenReference"/>
    /// </summary>
    /// <param name="envelope">The Xml element that contains the security token reference.</param>
    /// <param name="certificateRepository">Repository to obtain the certificate that is needed.</param>
    public IssuerSecurityTokenReference(XmlElement envelope, ICertificateRepository certificateRepository)
    {
        _certificateRepository = certificateRepository;
        _securityTokenReferenceId = $"STR-{Guid.NewGuid()}";

        LoadXml(envelope);
    }

    protected override X509Certificate2 LoadCertificate()
    {
        if (string.IsNullOrWhiteSpace(_certificateSerialNr))
        {
            throw new CryptographicException("Unable to retrieve Certificate: No X509SerialNumber available.");
        }

        if (_certificateRepository == null)
        {
            throw new CryptographicException("Unable to retrieve Certificate: No CertificateRepository defined.");
        }

        return _certificateRepository.GetCertificate(X509FindType.FindBySerialNumber, _certificateSerialNr);
    }

    /// <summary>
    /// Load the <see cref="X509Certificate2" />
    /// from the given <paramref name="element" />
    /// </summary>
    /// <param name="element"></param>
    public override void LoadXml(XmlElement element)
    {
        var ns = new XmlNamespaceManager(new NameTable());
        ns.AddNamespace("wsse", Constants.Namespaces.WssSecuritySecExt);
        ns.AddNamespace("dsig", Constants.Namespaces.XmlDsig);

        var xmlIssuerSerial =
            element.SelectSingleNode("//wsse:SecurityTokenReference/dsig:X509Data/dsig:X509IssuerSerial/dsig:X509SerialNumber", ns) as XmlElement ?? throw new XmlException(
                $"No <dsig:X509SerialNumber/> element found in <wsse:SecurityTokenReference/> element");
        _certificateSerialNr = xmlIssuerSerial.InnerText;
    }

    /// <summary>
    /// When overridden in a derived class, returns an XML representation of the
    /// <see cref="T:System.Security.Cryptography.Xml.KeyInfoClause" />.
    /// </summary>
    /// <returns>An XML representation of the <see cref="T:System.Security.Cryptography.Xml.KeyInfoClause" />.</returns>
    public override XmlElement GetXml()
    {
        var xmlDocument = new XmlDocument { PreserveWhitespace = true };

        var securityTokenReferenceElement = xmlDocument.CreateElement(
            prefix: "wsse",
            localName: "SecurityTokenReference",
            namespaceURI: Constants.Namespaces.WssSecuritySecExt);

        securityTokenReferenceElement.SetAttribute(
            localName: "Id",
            namespaceURI: Constants.Namespaces.WssSecurityUtility,
            value: _securityTokenReferenceId);

        var x509DataElement = xmlDocument.CreateElement(
            prefix: "ds",
            localName: "X509Data",
            namespaceURI: Constants.Namespaces.XmlDsig);
        securityTokenReferenceElement.AppendChild(x509DataElement);

        var x509IssuerSerialElement = xmlDocument.CreateElement(
            prefix: "ds",
            localName: "X509IssuerSerial",
            namespaceURI: Constants.Namespaces.XmlDsig);
        x509DataElement.AppendChild(x509IssuerSerialElement);

        var x509SerialNumberElement = xmlDocument.CreateElement(
           prefix: "ds",
           localName: "X509SerialNumber",
           namespaceURI: Constants.Namespaces.XmlDsig);

        if (Certificate != null)
        {
            var x509IssuerNameElement = xmlDocument.CreateElement(
                prefix: "ds",
                localName: "X509IssuerName",
                namespaceURI: Constants.Namespaces.XmlDsig);
            x509IssuerSerialElement.AppendChild(x509IssuerNameElement);

            var issuerNameName = Certificate.IssuerName.Name;
            if (issuerNameName != null)
            {
                x509IssuerNameElement.InnerText = issuerNameName;
            }

            x509SerialNumberElement.InnerText = GetIssuerSerialNumberFromCertificate(Certificate);
        }
        else if (!string.IsNullOrWhiteSpace(_certificateSerialNr))
        {
            x509SerialNumberElement.InnerText = _certificateSerialNr;
        }

        x509IssuerSerialElement.AppendChild(x509SerialNumberElement);

        return securityTokenReferenceElement;
    }

    private static string GetIssuerSerialNumberFromCertificate(X509Certificate2 certificate)
    {
        try
        {
            BigInteger.TryParse(
                value: certificate.SerialNumber,
                style: NumberStyles.AllowHexSpecifier,
                provider: CultureInfo.InvariantCulture,
                result: out var serialNumber);

            return serialNumber.ToString();
        }
        catch (Exception ex)
        {
            throw new CryptographicException(
                $"Failed to convert certificate serial number {certificate.SerialNumber} to big integer", ex);
        }
    }
}
