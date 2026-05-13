using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Security.References;
using Eu.EDelivery.AS4.Security.Signing;
using Eu.EDelivery.AS4.Security.Transforms;
using Reference = System.Security.Cryptography.Xml.Reference;

namespace Eu.EDelivery.AS4.Security.Strategies;

internal class SignatureVerificationStrategy : SignatureStrategy
{
    private readonly XmlDocument _soapEnvelope;

    internal SignatureVerificationStrategy(XmlDocument soapEnvelope) : base(soapEnvelope)
    {
        if (!SafeCanonicalizationMethods.Contains(AttachmentSignatureTransform.Url))
        {
            SafeCanonicalizationMethods.Add(AttachmentSignatureTransform.Url);
        }

        _soapEnvelope = soapEnvelope;

        LoadSignature();
    }

    /// <summary>
    /// Verify the Signature of the AS4 message
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    public bool VerifySignature(VerifySignatureConfig options)
    {
        var securityTokenReference = SecurityTokenReferenceProvider.Get(
            _soapEnvelope,
            SecurityTokenType.Signing,
            options.CertificateRepository);

        if (securityTokenReference?.Certificate is null)
        {
            throw new CryptographicException("The signing certificate is not found");
        }

        if (!VerifyCertificate(securityTokenReference.Certificate, options, out var status))
        {
            throw new CryptographicException($"The signing certificate is not trusted: {string.Join(" ", status.Select(s => s.StatusInformation))}");
        }

        LoadXml(GetSignatureElement());
        AddUnrecognizedAttachmentReferences(options.Attachments);

        var validSignature = CheckSignature(securityTokenReference.Certificate, verifySignatureOnly: true);

        foreach (var attachment in options.Attachments)
        {
            attachment.ResetContentPosition();
        }

        return validSignature;
    }

    private static bool VerifyCertificate(X509Certificate2 certificate, VerifySignatureConfig options, out X509ChainStatus[] errorMessages)
    {
        using var chain = new X509Chain();
        // TODO: Make this configurable
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = options.AllowUnknownRootCertificateAuthority ? X509VerificationFlags.AllowUnknownCertificateAuthority : X509VerificationFlags.NoFlag;
        chain.ChainPolicy.VerificationFlags |= options.AllowExpiredCertificate ? X509VerificationFlags.IgnoreNotTimeValid : X509VerificationFlags.NoFlag;

        var isValid = chain.Build(certificate);

        errorMessages = isValid ? [] : chain.ChainStatus;

        return isValid;
    }

    private void AddUnrecognizedAttachmentReferences(IEnumerable<Attachment> attachments)
    {
        if (SignedInfo is null)
        {
            throw new CryptographicException("SignedInfo is null");
        }

        var references = SignedInfo.References
            .Cast<Reference>()
            .Where(ReferenceIsCidReference())
            .ToArray();

        foreach (var reference in references)
        {
            var attachment = attachments.FirstOrDefault(a => a.Matches(reference));
            if (attachment is not null)
            {
                SetReferenceStream(reference, attachment);
                SetAttachmentTransformContentType(reference, attachment);
            }
        }
    }

    private static Func<Reference, bool> ReferenceIsCidReference()
    {
        return x => x?.Uri != null && x.Uri.StartsWith(CidPrefix) && x.Uri.Length > CidPrefix.Length;
    }
}
