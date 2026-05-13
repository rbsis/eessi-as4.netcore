using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Eu.EDelivery.AS4.Repositories;

namespace Eu.EDelivery.AS4.Security.References;

/// <summary>
/// Security Token Reference Strategy for the Key Identifier
/// </summary>
internal sealed class KeyIdentifierSecurityTokenReference : SecurityTokenReference
{
    private readonly ICertificateRepository? _certificateRepository;
    private string? _certificateSubjectKeyIdentifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyIdentifierSecurityTokenReference"/> class. 
    /// </summary>
    /// <param name="certificate">The Certificate for which a SecurityTokenReference must be created.</param>
    public KeyIdentifierSecurityTokenReference(X509Certificate2 certificate)
    {
        Certificate = certificate;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyIdentifierSecurityTokenReference"/> class.
    /// </summary>
    /// <param name="envelope">XML Element that contains a Key Identifier Security Token Reference.</param>
    /// <param name="certificateRepository">Repository to obtain the certificate needed to embed it into the Key Identifier Security Token Reference.</param>
    public KeyIdentifierSecurityTokenReference(XmlElement envelope, ICertificateRepository certificateRepository)
    {
        _certificateRepository = certificateRepository;
        LoadXml(envelope);
    }

    protected override X509Certificate2 LoadCertificate()
    {
        if (string.IsNullOrWhiteSpace(_certificateSubjectKeyIdentifier))
        {
            throw new CryptographicException("Unable to retrieve Certificate: No SubjectKeyIdentifier available.");
        }

        if (_certificateRepository == null)
        {
            throw new CryptographicException("Unable to retrieve Certificate: No CertificateRepository defined.");
        }

        return _certificateRepository.GetCertificate(
            X509FindType.FindBySubjectKeyIdentifier,
            _certificateSubjectKeyIdentifier);
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

        var xmlKeyIdentifier = element.SelectSingleNode("//wsse:SecurityTokenReference/wsse:KeyIdentifier", ns) as XmlElement ?? throw new XmlException(
                "No <wsse:KeyIdentifier/> element found in <wsse:SecurityTokenReference/> element");
        var base64Bytes = Convert.FromBase64String(xmlKeyIdentifier.InnerText);
        _certificateSubjectKeyIdentifier = Convert.ToHexString(base64Bytes);
    }

    /// <summary>
    /// Get the Xml for the Key Identifier
    /// </summary>
    /// <returns></returns>
    public override XmlElement GetXml()
    {
        var xmlDocument = new XmlDocument { PreserveWhitespace = true };

        var securityTokenReferenceElement = xmlDocument.CreateElement(
            prefix: "wsse",
            localName: "SecurityTokenReference",
            namespaceURI: Constants.Namespaces.WssSecuritySecExt);

        var keyIdentifierElement = xmlDocument.CreateElement(
            prefix: "wsse",
            localName: "KeyIdentifier",
            namespaceURI: Constants.Namespaces.WssSecuritySecExt);

        keyIdentifierElement.SetAttribute("EncodingType", Constants.Namespaces.Base64Binary);
        keyIdentifierElement.SetAttribute("ValueType", Constants.Namespaces.SubjectKeyIdentifier);
        keyIdentifierElement.InnerText = GetSubjectKeyIdentifier();

        securityTokenReferenceElement.AppendChild(keyIdentifierElement);
        return securityTokenReferenceElement;
    }

    private string GetSubjectKeyIdentifier()
    {
        if (!string.IsNullOrWhiteSpace(_certificateSubjectKeyIdentifier))
        {
            return _certificateSubjectKeyIdentifier;
        }

        if (Certificate != null)
        {
            foreach (var extension in Certificate.Extensions)
            {
                if (!string.Equals(extension.Oid?.FriendlyName, "Subject Key Identifier"))
                {
                    continue;
                }

                var x509SubjectKeyIdentifierExtension = extension as X509SubjectKeyIdentifierExtension;
                if (x509SubjectKeyIdentifierExtension?.SubjectKeyIdentifier is null)
                {
                    continue;
                }

                var base64Binary = Convert.FromHexString(x509SubjectKeyIdentifierExtension.SubjectKeyIdentifier);

                return Convert.ToBase64String(base64Binary);
            }
        }

        throw new CryptographicException(
            "No certificate or extension with the name 'Subject Key Identifier' was found in the certificate extensions");
    }
}
