using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace Eu.EDelivery.AS4.Security.References;

/// <summary>
/// Binary Security Token Strategy to add a Security Reference to the Message
/// </summary>
internal sealed class BinarySecurityTokenReference : SecurityTokenReference
{
    private byte[]? _certificateBytes;

    public string? ReferenceId { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinarySecurityTokenReference"/> class.
    /// </summary>
    public BinarySecurityTokenReference(X509Certificate2 certificate)
    {
        ReferenceId = "cert-" + Guid.NewGuid();
        Certificate = certificate;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BinarySecurityTokenReference"/> class.
    /// </summary>
    /// <param name="securityTokenElement">The 'Security' element to append the <see cref="BinarySecurityTokenReference"/>.</param>
    public BinarySecurityTokenReference(XmlElement securityTokenElement)
    {
        LoadXml(securityTokenElement);
    }

    protected override X509Certificate2? LoadCertificate()
    {
        if (_certificateBytes == null || !_certificateBytes.Any())
        {
            return null;
        }

        return new X509Certificate2(_certificateBytes);
    }

    /// <summary>
    /// Load the Xml from Element into Binary Security Token Reference
    /// </summary>
    /// <param name="element"></param>
    public override void LoadXml(XmlElement element)
    {
        var referenceElement = GetReferenceElement(element);
        if (referenceElement != null)
        {
            AssignReferenceId(referenceElement);
        }

        var binarySecurityTokenElement = GetBinarySecurityTokenElementFrom(element);
        if (binarySecurityTokenElement != null)
        {
            _certificateBytes = Convert.FromBase64String(binarySecurityTokenElement.InnerText);
        }
    }

    private static XmlElement? GetReferenceElement(XmlElement node)
    {
        return
            node.ChildNodes.OfType<XmlElement>()
                .FirstOrDefault(
                    e => e.LocalName == "Reference" && e.NamespaceURI == Constants.Namespaces.WssSecuritySecExt);
    }

    private void AssignReferenceId(XmlElement referenceElement)
    {
        if (!referenceElement.LocalName.Equals("Reference", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                @"The given XmlElement is not a Reference element",
                nameof(referenceElement));
        }

        var uriAttribute = referenceElement.Attributes["URI"];
        if (uriAttribute != null)
        {
            ReferenceId = uriAttribute.Value;
        }
    }

    private XmlElement? GetBinarySecurityTokenElementFrom(XmlNode node)
    {
        var securityHeader = node.ParentNode?.ParentNode?.ParentNode;
        return securityHeader?.ChildNodes.OfType<XmlElement>().FirstOrDefault(IsElementABinarySecurityTokenElement);
    }

    private bool IsElementABinarySecurityTokenElement(XmlElement x)
    {
        // Extra check on ReferenceId. 
        var idAttribute = x.Attributes["Id", Constants.Namespaces.WssSecurityUtility];

        var pureReferenceId = ReferenceId?.TrimStart('#');
        var pureAttributeId = idAttribute?.Value.Trim('#');

        return x.LocalName == "BinarySecurityToken" && pureAttributeId == pureReferenceId
               && x.NamespaceURI == Constants.Namespaces.WssSecuritySecExt;
    }

    /// <summary>
    /// Append the Security Token Reference for the Binary Security Token
    /// </summary>
    /// <param name="element"></param>
    /// <param name="document"></param>
    /// <returns></returns>
    public override XmlElement AppendSecurityTokenTo(XmlElement element, XmlDocument document)
    {
        var securityTokenElement = GetSecurityToken(document);
        element.AppendChild(securityTokenElement);

        return element;
    }

    private XmlElement GetSecurityToken(XmlDocument document)
    {
        var binarySecurityToken = document.CreateElement(
            "BinarySecurityToken",
            Constants.Namespaces.WssSecuritySecExt);

        SetBinarySecurityAttributes(binarySecurityToken);
        AppendCertificate(document, binarySecurityToken);

        return binarySecurityToken;
    }

    private void SetBinarySecurityAttributes(XmlElement binarySecurityToken)
    {
        binarySecurityToken.SetAttribute("EncodingType", Constants.Namespaces.Base64Binary);
        binarySecurityToken.SetAttribute("ValueType", Constants.Namespaces.ValueType);
        binarySecurityToken.SetAttribute("Id", Constants.Namespaces.WssSecurityUtility, ReferenceId);
    }

    private void AppendCertificate(XmlDocument document, XmlElement binarySecurityToken)
    {
        string? rawData = null;

        if (_certificateBytes != null && _certificateBytes.Length > 0)
        {
            rawData = Convert.ToBase64String(_certificateBytes);
        }
        else if (Certificate != null)
        {
            rawData = Convert.ToBase64String(Certificate.GetRawCertData());
        }

        if (!string.IsNullOrWhiteSpace(rawData))
        {
            XmlNode rawDataNode = document.CreateTextNode(rawData);
            binarySecurityToken.AppendChild(rawDataNode);
        }
    }

    /// <summary>
    /// Get Binary Security Token Reference Xml Element
    /// </summary>
    /// <returns></returns>
    public override XmlElement GetXml()
    {
        var xmlDocument = new XmlDocument { PreserveWhitespace = true };

        var securityTokenReferenceElement = xmlDocument.CreateElement(
            "SecurityTokenReference",
            Constants.Namespaces.WssSecuritySecExt);

        var referenceElement = xmlDocument.CreateElement("Reference", Constants.Namespaces.WssSecuritySecExt);
        securityTokenReferenceElement.AppendChild(referenceElement);
        SetReferenceSecurityAttributes(referenceElement);

        return securityTokenReferenceElement;
    }

    private void SetReferenceSecurityAttributes(XmlElement referenceElement)
    {
        referenceElement.SetAttribute("URI", "#" + ReferenceId);
        referenceElement.SetAttribute("ValueType", Constants.Namespaces.ValueType);
    }
}
