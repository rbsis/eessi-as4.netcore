using System.Security.Cryptography;
using Eu.EDelivery.AS4.Security.Transforms;

namespace Eu.EDelivery.AS4.Security.Algorithms;

/// <summary>
/// AES Symmetric Algorithm implementation
/// </summary>
public class AesGcmAlgorithm : SymmetricAlgorithm
{
    private readonly RandomNumberGenerator _randomNumberGenerator;

    /// <summary>
    /// Gets the block sizes, in bits, that are supported by the symmetric algorithm.
    /// </summary>
    /// <returns>An array that contains the block sizes supported by the algorithm.</returns>
    public override KeySizes[] LegalBlockSizes => [new KeySizes(minSize: 96, maxSize: 96, skipSize: 0)];

    /// <summary>
    /// Gets the key sizes, in bits, that are supported by the symmetric algorithm.
    /// </summary>
    /// <returns>An array that contains the key sizes supported by the algorithm.</returns>
    public override KeySizes[] LegalKeySizes => [new KeySizes(minSize: 96, maxSize: 256, skipSize: 32)];

    /// <summary>
    /// Initializes a new instance of the <see cref="AesGcmAlgorithm"/> class
    /// </summary>
    public AesGcmAlgorithm()
    {
        BlockSizeValue = 96;
        _randomNumberGenerator = RandomNumberGenerator.Create();
    }

    /// <summary>
    /// When overridden in a derived class, creates a symmetric encryptor object with the specified 
    /// <see cref="P:System.Security.Cryptography.SymmetricAlgorithm.Key" /> property and initialization vector 
    /// (<see cref="P:System.Security.Cryptography.SymmetricAlgorithm.IV" />).
    /// </summary>
    /// <returns>A symmetric encryptor object.</returns>
    /// <param name="rgbKey">The secret key to use for the symmetric algorithm. </param>
    /// <param name="rgbIV">The initialization vector to use for the symmetric algorithm. </param>
    public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[]? rgbIV)
    {
        ArgumentNullException.ThrowIfNull(rgbIV);
        return new AesGcmEncryptTransform(rgbKey, rgbIV);
    }

    /// <summary>
    /// When overridden in a derived class, creates a symmetric decryptor object with the specified 
    /// <see cref="P:System.Security.Cryptography.SymmetricAlgorithm.Key" /> property and initialization vector 
    /// (<see cref="P:System.Security.Cryptography.SymmetricAlgorithm.IV" />).
    /// </summary>
    /// <returns>A symmetric decryptor object.</returns>
    /// <param name="rgbKey">The secret key to use for the symmetric algorithm. </param>
    /// <param name="rgbIV">The initialization vector to use for the symmetric algorithm. </param>
    public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[]? rgbIV)
    {
        ArgumentNullException.ThrowIfNull(rgbIV);
        return new AesGcmDecryptTransform(rgbKey, rgbIV);
    }

    /// <summary>
    /// When overridden in a derived class, generates a random key 
    /// (<see cref="P:System.Security.Cryptography.SymmetricAlgorithm.Key" />) to use for the algorithm.
    /// </summary>
    public override void GenerateKey()
    {
        var generatedKey = new byte[KeySize / 8];
        _randomNumberGenerator.GetBytes(generatedKey);
        Key = generatedKey;
    }

    /// <summary>
    /// When overridden in a derived class, generates a random initialization vector 
    /// (<see cref="P:System.Security.Cryptography.SymmetricAlgorithm.IV" />) to use for the algorithm.
    /// </summary>
    public override void GenerateIV()
    {
        var generatedIv = new byte[BlockSize / 8];
        _randomNumberGenerator.GetBytes(generatedIv);
        IV = generatedIv;
    }
}
