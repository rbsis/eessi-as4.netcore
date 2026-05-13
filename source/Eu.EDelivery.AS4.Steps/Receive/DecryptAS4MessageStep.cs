using System.ComponentModel;
using System.Configuration;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Services.Journal;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;

namespace Eu.EDelivery.AS4.Steps.Receive;

/// <summary>
/// The use case describes how a message gets decrypted.
/// </summary>
[Info("Decrypt received message")]
[Description("Decrypts the received AS4 Message if necessary by using the specified Receiving PMode")]
public class DecryptAS4MessageStep : IStep
{
    private readonly ILogger<DecryptAS4MessageStep> _logger;
    private readonly ICertificateRepository _certificateRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="DecryptAS4MessageStep"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="certificateRepository">The certificate repository.</param>
    public DecryptAS4MessageStep(ILogger<DecryptAS4MessageStep> logger, ICertificateRepository certificateRepository)
    {
        _certificateRepository = certificateRepository;
        _logger = logger;
    }

    /// <summary>
    /// Start Decrypting <see cref="AS4Message"/>
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(messagingContext);

        if (messagingContext.AS4Message == null)
        {
            throw new InvalidOperationException(
                $"{nameof(DecryptAS4MessageStep)} requires a AS4Message to decrypt but no AS4Message is present in the MessagingContext");
        }

        if (messagingContext.AS4Message.IsSignalMessage)
        {
            _logger.LogDebug("AS4Message is SignalMessage so will skip decryption since AS4.NET Component only supports encryption of payloads");
            return await StepResult.SuccessAsync(messagingContext);
        }

        if (messagingContext.ReceivingPMode?.Security?.Decryption == null)
        {
            _logger.LogDebug("AS4Message will not be decrypted sicne ReceivingPMode hasn't got a Security.Decryption element");
            return await StepResult.SuccessAsync(messagingContext);
        }

        var receivePMode = messagingContext.ReceivingPMode;
        if (receivePMode.Security.Decryption.Encryption == Limit.Required && !messagingContext.AS4Message.IsEncrypted)
        {
            var message = "AS4Message is encrypted but ReceivingPMode {PModeId} doesn't allow it." + Environment.NewLine + " Please alter the PMode Decryption.Encryption element to Allowed or Ignored";
            _logger.LogError(message, receivePMode.Id);

            messagingContext.ErrorResult = new ErrorResult("AS4Message is not encrypted but ReceivingPMode requires it", ErrorAlias.PolicyNonCompliance);
            return await StepResult.FailedAsync(messagingContext);
        }

        if (receivePMode.Security.Decryption.Encryption == Limit.NotAllowed && messagingContext.AS4Message.IsEncrypted)
        {
            var message = "AS4Message is encrypted but ReceivingPMode {PModeId} doesn't allow it." + Environment.NewLine + " Please alter the PMode Decryption.Encryption element to Required, Allowed or Ignored";
            _logger.LogError(message, receivePMode.Id);

            messagingContext.ErrorResult = new ErrorResult("AS4Message is encrypted but ReceivingPMode doesn't allow it", ErrorAlias.PolicyNonCompliance);
            return await StepResult.FailedAsync(messagingContext);
        }

        if (!messagingContext.AS4Message.IsEncrypted)
        {
            _logger.LogDebug("AS4Message is not encrypted so will skip decryption");
            return await StepResult.SuccessAsync(messagingContext);
        }

        if (messagingContext.ReceivingPMode?.Security?.Decryption?.Encryption == Limit.Ignored)
        {
            _logger.LogDebug("Decryption is ignored in ReceivingPMode {PModeId}, so no decryption will take place", receivePMode.Id);
            return await StepResult.SuccessAsync(messagingContext);
        }

        return await DecryptAS4MessageAsync(messagingContext);
    }

    private async Task<StepResult> DecryptAS4MessageAsync(MessagingContext messagingContext)
    {
        try
        {
            _logger.LogTrace("Start decrypting AS4Message ...");
            var decryptionCertificate = GetCertificate(messagingContext);
            messagingContext.AS4Message!.Decrypt(decryptionCertificate);
            _logger.LogInformation("{LogTag} AS4Message is decrypted correctly", messagingContext.LogTag);

            var entry = JournalLogEntry.CreateFrom(
                messagingContext.AS4Message,
                $"Decrypted using certificate {decryptionCertificate.FriendlyName}");

            var result = await StepResult.SuccessAsync(messagingContext);
            _logger.LogTrace("Append log to message journal: {LogEntries}", string.Join(", ", entry.LogEntries));
            return await result.WithJournalAsync(entry);
        }
        catch (Exception ex) when (ex is CryptoException || ex is CryptographicException)
        {
            _logger.LogError(ex, "Decryption of message failed");

            messagingContext.ErrorResult = new ErrorResult(
                description: "Decryption of message failed",
                alias: ErrorAlias.FailedDecryption);
            return await StepResult.FailedAsync(messagingContext);
        }
    }

    private X509Certificate2 GetCertificate(MessagingContext messagingContext)
    {
        var decryption = messagingContext.ReceivingPMode!.Security.Decryption;

        if (decryption.DecryptCertificateInformation == null)
        {
            throw new ConfigurationErrorsException(
                "Cannot start decrypting: no certificate information found " +
                $"in ReceivingPMode {messagingContext.ReceivingPMode.Id} to decrypt the message. " +
                "Please use either a <CertificateFindCriteria/> or <PrivateKeyCertificate/> to specify the certificate information");
        }

        if (decryption.DecryptCertificateInformation is CertificateFindCriteria certFindCriteria)
        {
            return _certificateRepository.GetCertificate(
                certFindCriteria.CertificateFindType,
                certFindCriteria.CertificateFindValue);
        }

        if (decryption.DecryptCertificateInformation is PrivateKeyCertificate embeddedCertInfo
            && embeddedCertInfo.Certificate is not null)
        {
            return new X509Certificate2(
                rawData: Convert.FromBase64String(embeddedCertInfo.Certificate),
                password: embeddedCertInfo.Password,
                keyStorageFlags: X509KeyStorageFlags.Exportable
                                 | X509KeyStorageFlags.MachineKeySet
                                 | X509KeyStorageFlags.PersistKeySet);
        }

        throw new NotSupportedException(
            "The decrypt-certificate information specified in the ReceivingPMode " +
            $"{messagingContext.ReceivingPMode.Id} could not be used to retrieve the certificate used for decryption. " +
            "Please use either a <CertificateFindCriteria/> or <PrivateKeyCertificate/> to specify the certificate information");
    }
}
