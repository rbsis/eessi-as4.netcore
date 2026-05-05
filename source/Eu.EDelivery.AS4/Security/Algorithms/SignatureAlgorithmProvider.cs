using System.Xml;
using Eu.EDelivery.AS4.Extensions;

namespace Eu.EDelivery.AS4.Security.Algorithms;

/// <summary>
/// Class to provide <see cref="SignatureAlgorithm" /> implementations
/// </summary>
public static class SignatureAlgorithmProvider
{
    private static readonly Dictionary<string, SignatureAlgorithm> _algorithms = new()
    {
        ["http://www.w3.org/2001/04/xmldsig-more#rsa-sha256"] = new RsaPkCs1Sha256SignatureAlgorithm(),
        ["http://www.w3.org/2001/04/xmldsig-more#rsa-sha384"] = new RsaPkCs1Sha384SignatureDescription(),
        ["http://www.w3.org/2001/04/xmldsig-more#rsa-sha512"] = new RsaPkCs1Sha512SignatureAlgorithm()
    };

    /// <summary>
    /// Get a <see cref="SignatureAlgorithm" /> implementation
    /// based on a algorithm namespace
    /// </summary>
    /// <param name="algorithmNamespace"></param>
    /// <returns></returns>
    public static SignatureAlgorithm Get(string algorithmNamespace)
    {
        return _algorithms.ReadMandatoryProperty(algorithmNamespace);
    }

    public static SignatureAlgorithm Get(XmlDocument envelopeDocument)
    {
        var xmlSignatureElement = GetSignatureElement(envelopeDocument);
        var algorithmAttribute = GetSignatureAlgorithm(xmlSignatureElement);

        return _algorithms[algorithmAttribute];
    }

    private static XmlElement GetSignatureElement(XmlNode envelopeDocument)
    {
        if (envelopeDocument.SelectSingleNode("//*[local-name()='SignatureMethod']") is not XmlElement xmlSignatureElement)
        {
            throw new XmlException("No SignatureMethod XmlElement found in given Envelope Document");
        }

        return xmlSignatureElement;
    }

    private static string GetSignatureAlgorithm(XmlElement xmlSignatureElement)
    {
        var algorithmAttribute = xmlSignatureElement.GetAttribute("Algorithm");

        if (!_algorithms.ContainsKey(algorithmAttribute))
        {
            throw new KeyNotFoundException($"No given Signature Algorithm found for: {algorithmAttribute}");
        }

        return algorithmAttribute;
    }
}
