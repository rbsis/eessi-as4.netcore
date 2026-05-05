using System.ComponentModel;
using System.Configuration;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Security.Encryption;
using Eu.EDelivery.AS4.Services.Journal;
using Microsoft.Extensions.Logging;
using InvalidOperationException = System.InvalidOperationException;

namespace Eu.EDelivery.AS4.Steps.Send;

/// <summary>
/// Describes how the MSH encrypts the ebMS UserMessage
/// </summary>
[Info("Encrypt AS4 Message if necessary")]
[Description("This step encrypts the AS4 Message and its attachments if encryption is enabled in the Sending PMode")]
public class EncryptAS4MessageStep : IStep
{
    private readonly ILogger<EncryptAS4MessageStep> _logger;
    private readonly ICertificateRepository _certificateRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptAS4MessageStep"/> class
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="certificateRepository"></param>
    public EncryptAS4MessageStep(ILogger<EncryptAS4MessageStep> logger, ICertificateRepository certificateRepository)
    {
        _certificateRepository = certificateRepository;
        _logger = logger;
    }

    /// <summary>
    /// Start Encrypting AS4 Message
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
                $"{nameof(EncryptAS4MessageStep)} requires an AS4Message to encrypt but no AS4Message is present in the MessagingContext");
        }

        if (messagingContext.SendingPMode == null)
        {
            throw new InvalidOperationException(
                $"{nameof(EncryptAS4MessageStep)} requires a SendingPMode to encrypt the AS4Message but no SendingPMode is present in the MessagingContext");
        }

        if (messagingContext.SendingPMode.Security?.Encryption == null
            || !messagingContext.SendingPMode.Security.Encryption.IsEnabled)
        {
            _logger.LogTrace(
                "No encryption of the AS4Message will happen because the " +
                "SendingPMode {PModeId} Security.Encryption.IsEnabled is disabled",
                messagingContext.SendingPMode?.Id);

            return await StepResult.SuccessAsync(messagingContext);
        }

        _logger.LogInformation(
            "(Outbound)[{PrimaryMessageId}] Encrypt AS4Message with given encryption information " +
            "configured in the SendingPMode: {PModeId}",
            messagingContext.AS4Message.GetPrimaryMessageId(),
            messagingContext.SendingPMode.Id);

        var keyEncryptionConfig = RetrieveKeyEncryptionConfig(messagingContext.SendingPMode);
        var encryptionSettings = messagingContext.SendingPMode.Security.Encryption;
        var dataEncryptionConfig = new DataEncryptionConfiguration(
            encryptionMethod: encryptionSettings.Algorithm,
            algorithmKeySize: encryptionSettings.AlgorithmKeySize);

        EncryptAS4Message(
            messagingContext.AS4Message,
            keyEncryptionConfig,
            dataEncryptionConfig);

        var entry = JournalLogEntry.CreateFrom(
            messagingContext.AS4Message,
            $"Encrypted using certificate {keyEncryptionConfig.EncryptionCertificate.FriendlyName} and "
            + $"key encryption method: {keyEncryptionConfig.EncryptionMethod}, key digest method: {keyEncryptionConfig.DigestMethod}, "
            + $"key mgf: {keyEncryptionConfig.Mgf} and Data encryption method: {dataEncryptionConfig.EncryptionMethod}, "
            + $" data encryption type: {dataEncryptionConfig.EncryptionType}, data transport algorithm: {dataEncryptionConfig.TransformAlgorithm}");

        var result = await StepResult.SuccessAsync(messagingContext);
        _logger.LogTrace("Append log to message journal: {LogEntries}", string.Join(", ", entry.LogEntries));
        return await result.WithJournalAsync(entry);
    }

    private void EncryptAS4Message(
        AS4Message message,
        KeyEncryptionConfiguration keyEncryptionConfig,
        DataEncryptionConfiguration dataEncryptionConfig)
    {
        try
        {
            message.Encrypt(keyEncryptionConfig, dataEncryptionConfig);
        }
        catch (Exception exception)
        {
            var description = $"Problems with encryption AS4Message: {exception}";
            _logger.LogError(exception, description);

            throw new CryptographicException(description, exception);
        }
    }

    private KeyEncryptionConfiguration RetrieveKeyEncryptionConfig(SendingProcessingMode pmode)
    {
        var certificate = RetrieveCertificate(pmode);

        return new KeyEncryptionConfiguration(
            encryptionCertificate: certificate,
            keyEncryption: pmode.Security.Encryption.KeyTransport);
    }

    private X509Certificate2 RetrieveCertificate(SendingProcessingMode pmode)
    {
        var encryptionSettings = pmode.Security.Encryption;
        if (encryptionSettings.EncryptionCertificateInformation == null)
        {
            throw new ConfigurationErrorsException(
                $"No encryption certificate information found in SendingPMode {pmode.Id} to perform encryption");
        }

        if (encryptionSettings.EncryptionCertificateInformation is CertificateFindCriteria certFindCriteria)
        {
            return _certificateRepository.GetCertificate(
                certFindCriteria.CertificateFindType,
                certFindCriteria.CertificateFindValue);
        }

        if (encryptionSettings.EncryptionCertificateInformation is PublicKeyCertificate pubKeyCert
            && pubKeyCert.Certificate is not null)
        {
            return new X509Certificate2(Convert.FromBase64String(pubKeyCert.Certificate), string.Empty);
        }

        throw new NotSupportedException(
            $"The encryption certificate information specified in the Sending PMode {pmode.Id} could not be used to retrieve the certificate");
    }
}
