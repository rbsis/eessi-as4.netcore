using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Eu.EDelivery.AS4.Repositories;

namespace Eu.EDelivery.AS4.Security.References;

internal static class SecurityTokenReferenceProvider
{
    public static SecurityTokenReference Create(X509Certificate2 certificate, X509ReferenceType referenceType) => referenceType switch
    {
        X509ReferenceType.BSTReference => new BinarySecurityTokenReference(certificate),
        X509ReferenceType.IssuerSerial => new IssuerSecurityTokenReference(certificate),
        X509ReferenceType.KeyIdentifier => new KeyIdentifierSecurityTokenReference(certificate),
        _ => new BinarySecurityTokenReference(certificate),
    };

    public static SecurityTokenReference? Get(XmlDocument envelopeDocument, SecurityTokenType type, ICertificateRepository certificateRepository)
    {
        var keyInfoElement = type switch
        {
            SecurityTokenType.Signing => envelopeDocument.SelectSingleNode(
                @"//*[local-name()='Header']/*[local-name()='Security']/*[local-name()='Signature']/*[local-name()='KeyInfo']/*[local-name()='SecurityTokenReference']") as XmlElement,
            SecurityTokenType.Encryption => envelopeDocument.SelectSingleNode(
                @"//*[local-name()='Header']/*[local-name()='Security']/*[local-name()='EncryptedKey']/*[local-name()='KeyInfo']/*[local-name()='SecurityTokenReference']") as XmlElement,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        if (keyInfoElement == null)
        {
            return null;
        }

        if (HasEnvelopeTag(keyInfoElement, securityTokenNodeName: "Reference"))
        {
            return new BinarySecurityTokenReference(keyInfoElement);
        }

        if (HasEnvelopeTag(keyInfoElement, securityTokenNodeName: "KeyIdentifier"))
        {
            return new KeyIdentifierSecurityTokenReference(keyInfoElement, certificateRepository);
        }

        if (HasEnvelopeTag(keyInfoElement, securityTokenNodeName: "X509Data"))
        {
            return new IssuerSecurityTokenReference(keyInfoElement, certificateRepository);
        }

        throw new NotSupportedException("Unable to retrieve SecurityTokenReference of type " + keyInfoElement.OuterXml);
    }

    private static bool HasEnvelopeTag(XmlNode element, string securityTokenNodeName)
    {
        return element?.SelectSingleNode($"./*[local-name()='{securityTokenNodeName}']") != null;
    }
}

public enum SecurityTokenType
{
    Signing,
    Encryption
}
