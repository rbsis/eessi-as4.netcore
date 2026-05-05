using System.Security.Cryptography.Xml;
using System.Xml;
using Eu.EDelivery.AS4.Security.References;
using Eu.EDelivery.AS4.Security.Strategies;

namespace Eu.EDelivery.AS4.Model.Core;

/// <summary>
/// WS Security Signed Xml
/// </summary>
public class SecurityHeader
{
    public bool IsSigned { get; private set; }
    public bool IsEncrypted { get; private set; }

    private XmlElement? _securityHeaderElement;

    private Signature? _signature;

    private XmlNodeList? _encryptionElements;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityHeader"/> class. 
    /// Create empty <see cref="SecurityHeader"/>
    /// </summary>
    public SecurityHeader() { }

    public SecurityHeader(XmlElement securityHeaderElement)
    {
        _securityHeaderElement = securityHeaderElement;

        if (_securityHeaderElement != null)
        {
            var nsMgr = GetNamespaceManager(_securityHeaderElement.OwnerDocument);

            IsSigned = _securityHeaderElement.SelectSingleNode("//ds:Signature", nsMgr) != null;
            IsEncrypted = _securityHeaderElement.SelectSingleNode("//xenc:EncryptedData", nsMgr) != null;
        }
        else
        {
            IsSigned = false;
            IsEncrypted = false;
        }
    }


    /// <summary>
    /// Sign using the given <paramref name="signingStrategy"/>
    /// </summary>
    /// <param name="signingStrategy"></param>
    internal void Sign(SignStrategy signingStrategy)
    {
        _signature = signingStrategy.SignDocument();

        IsSigned = true;
    }

    /// <summary>
    /// Encrypts the message and its attachments.
    /// </summary>
    /// <param name="encryptionStrategy"></param>
    internal void Encrypt(EncryptionStrategy encryptionStrategy)
    {
        encryptionStrategy.EncryptMessage();
        IsEncrypted = true;

        var securityHeader = CreateSecurityHeaderElement();

        encryptionStrategy.AppendEncryptionElements(securityHeader);

        _encryptionElements = securityHeader.ChildNodes;
    }

    /// <summary>
    /// Decrypts the message and its attachments.
    /// </summary>
    /// <param name="decryptionStrategy"></param>
    internal void Decrypt(DecryptionStrategy decryptionStrategy)
    {
        decryptionStrategy.DecryptMessage();
        IsEncrypted = false;
        _encryptionElements = null;

        RemoveExistingEncryptionElements();
    }

    private void RemoveExistingEncryptionElements()
    {
        if (_securityHeaderElement == null)
        {
            return;
        }

        var nsMgr = GetNamespaceManager(_securityHeaderElement.OwnerDocument);

        var encryptedKeyNode = _securityHeaderElement.SelectSingleNode("//wsse:Security/xenc:EncryptedKey", nsMgr);
        var encryptedDataNodes = _securityHeaderElement.SelectNodes("//wsse:Security/xenc:EncryptedData", nsMgr);

        if (encryptedKeyNode != null)
        {
            _securityHeaderElement.RemoveChild(encryptedKeyNode);
        }

        if (encryptedDataNodes != null)
        {
            foreach (XmlNode encryptedDataNode in encryptedDataNodes)
            {
                _securityHeaderElement.RemoveChild(encryptedDataNode);
            }
        }
    }

    /// <summary>
    /// Gets the full Security XML element.
    /// </summary>
    /// <returns></returns>
    public XmlElement? GetXml()
    {
        if (_securityHeaderElement == null && _signature == null && _encryptionElements == null)
        {
            return null;
        }

        _securityHeaderElement ??= CreateSecurityHeaderElement();

        // Append the encryption elements as first
        InsertNewEncryptionElements();

        // Signature elements should occur last in the header.
        InsertNewSignatureElements();

        return _securityHeaderElement;
    }

    private static XmlElement CreateSecurityHeaderElement()
    {
        var xmlDocument = new XmlDocument() { PreserveWhitespace = true };

        var securityHeaderElement = xmlDocument.CreateElement("wsse", "Security", Constants.Namespaces.WssSecuritySecExt);
        xmlDocument.AppendChild(securityHeaderElement);

        return securityHeaderElement;
    }

    private void InsertNewEncryptionElements()
    {
        if (_securityHeaderElement == null || _encryptionElements == null)
        {
            return;
        }

        // Encryption elements must occur as the first items in the list.
        var referenceNode = _securityHeaderElement.ChildNodes.OfType<XmlNode>().FirstOrDefault();

        foreach (XmlNode encryptionElement in _encryptionElements)
        {
            var nodeToImport = _securityHeaderElement.OwnerDocument.ImportNode(encryptionElement, deep: true);
            _securityHeaderElement.InsertBefore(nodeToImport, referenceNode);
        }

        _encryptionElements = null;
    }

    private void InsertNewSignatureElements()
    {
        if (_securityHeaderElement == null || _signature == null)
        {
            return;
        }

        // The SecurityToken that was used for the signature must occur before the 
        // signature and its references.
        foreach (var reference in _signature.KeyInfo.OfType<SecurityTokenReference>())
        {
            reference.AppendSecurityTokenTo(_securityHeaderElement, _securityHeaderElement.OwnerDocument);
        }

        var signatureElement = _signature.GetXml();
        signatureElement = _securityHeaderElement.OwnerDocument.ImportNode(signatureElement, deep: true) as XmlElement;
        if (signatureElement != null)
        {
            _securityHeaderElement.AppendChild(signatureElement);
        }

        _signature = null;
    }

    /// <summary>
    /// Get the Signed References from the signature.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<System.Security.Cryptography.Xml.Reference> GetReferences()
    {
        // TODO: this must be improved.

        var securityHeader = GetXml();
        if (securityHeader == null)
        {
            return [];
        }

        var signature = new SignatureVerificationStrategy(securityHeader.OwnerDocument);
        if (signature.SignedInfo == null)
        {
            return [];
        }

        return signature.SignedInfo.References.OfType<System.Security.Cryptography.Xml.Reference>();
    }

    private static XmlNamespaceManager GetNamespaceManager(XmlDocument xmlDocument)
    {
        var nsMgr = new XmlNamespaceManager(xmlDocument.NameTable);

        nsMgr.AddNamespace("ds", Constants.Namespaces.XmlDsig);
        nsMgr.AddNamespace("xenc", Constants.Namespaces.XmlEnc);
        nsMgr.AddNamespace("wsse", Constants.Namespaces.WssSecuritySecExt);

        return nsMgr;
    }
}
