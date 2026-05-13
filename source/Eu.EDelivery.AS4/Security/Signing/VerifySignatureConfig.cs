using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Repositories;

namespace Eu.EDelivery.AS4.Security.Signing;

/// <summary>
/// Configuration Options for
/// the verification of the <see cref="AS4Message"/>
/// </summary>
public class VerifySignatureConfig
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VerifySignatureConfig"/> class.
    /// </summary>
    /// <param name="allowUnknownRootCertificateAuthority">if set to <c>true</c> [allow unknown root certificate authority].</param>
    /// <param name="allowExpiredCertificate"></param>
    /// <param name="attachments">The attachments.</param>
    /// <param name="certificateRepository">The certificate repository used to retrieve the signing certificate that was referenced in the message.</param>
    public VerifySignatureConfig(
        bool allowUnknownRootCertificateAuthority,
        bool allowExpiredCertificate,
        IEnumerable<Attachment> attachments,
        ICertificateRepository certificateRepository)
    {
        AllowUnknownRootCertificateAuthority = allowUnknownRootCertificateAuthority;
        AllowExpiredCertificate = allowExpiredCertificate;
        Attachments = attachments ?? [];
        CertificateRepository = certificateRepository;
    }

    public bool AllowUnknownRootCertificateAuthority { get; }

    public bool AllowExpiredCertificate { get; }

    public IEnumerable<Attachment> Attachments { get; }

    public ICertificateRepository CertificateRepository { get; }
}
