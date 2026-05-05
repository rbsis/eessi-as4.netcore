using System.Security.Cryptography;

namespace Eu.EDelivery.AS4.Security.Algorithms;

/// <summary>
/// Declare the signature type for rsa sha256
/// </summary>
public class RsaPkCs1Sha256SignatureAlgorithm : SignatureAlgorithm
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RsaPkCs1Sha256SignatureAlgorithm"/> class. 
    /// Create new RSA SHA 256 Algorithm with 
    /// setted Key/Digest/(De)Formatter Algorithms
    /// </summary>
    public RsaPkCs1Sha256SignatureAlgorithm()
    {
        KeyAlgorithm = "System.Security.Cryptography.RSACryptoServiceProvider";
        FormatterAlgorithm = "System.Security.Cryptography.RSAPKCS1SignatureFormatter";
        DeformatterAlgorithm = "System.Security.Cryptography.RSAPKCS1SignatureDeformatter";
    }

    /// <summary>
    /// Get the Identifier of the Signature Algorithm
    /// </summary>
    /// <returns></returns>
    public override string GetIdentifier()
    {
        return "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    }

    /// <summary>
    /// Create an <see cref="AsymmetricSignatureDeformatter"/> from a <see cref="AsymmetricAlgorithm"/>
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public override AsymmetricSignatureDeformatter CreateDeformatter(AsymmetricAlgorithm key)
    {
        var asymmetricSignatureDeformatter = CryptoConfig.CreateFromName(DeformatterAlgorithm!) as AsymmetricSignatureDeformatter
            ?? throw new InvalidOperationException("CreateDeformatter failed");

        asymmetricSignatureDeformatter.SetKey(key);
        asymmetricSignatureDeformatter.SetHashAlgorithm("SHA256");

        return asymmetricSignatureDeformatter;
    }

    public override AsymmetricSignatureFormatter CreateFormatter(AsymmetricAlgorithm key)
    {
        var sigProcessor = CryptoConfig.CreateFromName(FormatterAlgorithm!) as AsymmetricSignatureFormatter
             ?? throw new InvalidOperationException("CreateFormatter failed");

        sigProcessor.SetKey(key);
        sigProcessor.SetHashAlgorithm("SHA256");

        return sigProcessor;
    }

    public override HashAlgorithm CreateDigest() => SHA256.Create();
}
