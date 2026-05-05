using System.Security.Cryptography.X509Certificates;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Security.Strategies;

namespace Eu.EDelivery.AS4.Builders.Security;

/// <summary>
/// Builder used to create an <see cref="DecryptionStrategy"/> instance.
/// </summary>
internal class DecryptionStrategyBuilder
{
    private readonly AS4Message _message;

    private readonly X509Certificate2 _certificate;

    private DecryptionStrategyBuilder(AS4Message message, X509Certificate2 certificate)
    {
        _message = message;
        _certificate = certificate;
    }

    /// <summary>
    /// Create a builder instance for the given <paramref name="as4Message"/>
    /// </summary>
    /// <param name="as4Message"></param>
    /// <param name="certificate"></param>
    /// <returns></returns>
    public static DecryptionStrategyBuilder Create(AS4Message as4Message, X509Certificate2 certificate)
    {
        return new DecryptionStrategyBuilder(as4Message, certificate);
    }

    /// <summary>
    /// Build the IDecryptionStrategy implementation.
    /// </summary>
    /// <returns></returns>
    public DecryptionStrategy Build()
    {
        if (_message.EnvelopeDocument == null)
        {
            throw new InvalidOperationException("EnvelopeDocument is missing.");
        }
        return new DecryptionStrategy(_message.EnvelopeDocument, _message.Attachments, _certificate);
    }
}
