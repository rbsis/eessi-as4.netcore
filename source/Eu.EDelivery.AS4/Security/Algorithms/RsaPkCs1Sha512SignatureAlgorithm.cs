using System.Security.Cryptography;

namespace Eu.EDelivery.AS4.Security.Algorithms;

/// <summary>
/// Declare the signature type for rsa-sha512
/// </summary>
public class RsaPkCs1Sha512SignatureAlgorithm : SignatureAlgorithm
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RsaPkCs1Sha512SignatureAlgorithm"/> class. 
    /// Create a new RSA SHA 384 Algorithm with 
    /// setted Key/Digest/(De)Formatter Algorithms
    /// </summary>
    public RsaPkCs1Sha512SignatureAlgorithm()
    {
        KeyAlgorithm = typeof(RSACryptoServiceProvider).FullName;
        FormatterAlgorithm = typeof(RSAPKCS1SignatureFormatter).FullName;
        DeformatterAlgorithm = typeof(RSAPKCS1SignatureDeformatter).FullName;
    }

    /// <summary>
    /// Get the Identifier of the Signature Algorithm
    /// </summary>
    /// <returns></returns>
    public override string GetIdentifier()
    {
        return "http://www.w3.org/2001/04/xmldsig-more#rsa-sha512";
    }

    /// <summary>
    /// Create an <see cref="AsymmetricSignatureDeformatter"/> from a <see cref="AsymmetricAlgorithm"/>
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public override AsymmetricSignatureDeformatter CreateDeformatter(AsymmetricAlgorithm key)
    {
        var sigProcessor = CryptoConfig.CreateFromName(DeformatterAlgorithm!) as AsymmetricSignatureDeformatter
             ?? throw new InvalidOperationException("CreateDeformatter failed");

        sigProcessor.SetKey(key);
        sigProcessor.SetHashAlgorithm("SHA512");

        return sigProcessor;
    }

    public override AsymmetricSignatureFormatter CreateFormatter(AsymmetricAlgorithm key)
    {
        var sigProcessor = CryptoConfig.CreateFromName(FormatterAlgorithm!) as AsymmetricSignatureFormatter
             ?? throw new InvalidOperationException("CreateFormatter failed");

        sigProcessor.SetKey(key);
        sigProcessor.SetHashAlgorithm("SHA512");

        return sigProcessor;
    }

    public override HashAlgorithm CreateDigest() => SHA512.Create();
}
