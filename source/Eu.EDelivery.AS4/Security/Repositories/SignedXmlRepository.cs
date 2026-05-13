using System.Security.Cryptography;
using System.Xml;

namespace Eu.EDelivery.AS4.Security.Repositories;

/// <summary>
/// Respository to navigate the Reference ID Xml Elements
/// </summary>
internal class SignedXmlRepository
{
    private static readonly string[] _allowedIdNodeNames = ["Id", "id", "ID"];
    private readonly XmlDocument _document;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignedXmlRepository" /> class
    /// </summary>
    /// <param name="document"></param>
    public SignedXmlRepository(XmlDocument document)
    {
        _document = document;
    }

    /// <summary>
    /// Get the <see cref="XmlElement" /> which
    /// references the given <paramref name="id" />
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public XmlElement? GetReferenceIdElement(string id)
    {
        return (from idNodeName in _allowedIdNodeNames
                select FindIdElements(id, idNodeName)
                into matchingNodes
                where !MatchingNodesIsNotPopulated(matchingNodes)
                select matchingNodes.Single()).FirstOrDefault();
    }

    private List<XmlElement> FindIdElements(string idValue, string idNodeName)
    {
        // SECURE: Validate idNodeName against whitelist to prevent XPath injection
        if (!_allowedIdNodeNames.Contains(idNodeName))
        {
            throw new ArgumentException("Invalid ID node name", nameof(idNodeName));
        }

        // SECURE: Use string.Format with validated parameters
        var xpath = string.Format(
            "//*[@*[local-name()='{0}' and namespace-uri()='{1}' and .='{2}']]",
            idNodeName,
            Constants.Namespaces.WssSecurityUtility,
            idValue);

        return _document.SelectNodes(xpath)?.Cast<XmlElement>().ToList() ?? [];
    }

    private static bool MatchingNodesIsNotPopulated(IReadOnlyCollection<XmlElement> matchingNodes)
    {
        if (matchingNodes.Count <= 0)
        {
            return true;
        }

        if (matchingNodes.Count >= 2)
        {
            throw new CryptographicException("Malformed reference element.");
        }

        return false;
    }

    /// <summary>
    /// Get the <see cref="XmlElement" /> which
    /// contains the Signature
    /// </summary>
    /// <returns></returns>
    public XmlElement GetSignatureElement()
    {
        var nodeSignature = _document.SelectSingleNode("//*[local-name()='Signature'] ");
        if (nodeSignature is not XmlElement xmlSignature)
        {
            throw new CryptographicException("Invalid Signature: Signature Tag not found");
        }

        return xmlSignature;
    }
}
