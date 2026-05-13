using System.ComponentModel;
using System.Configuration;
using System.Security.Cryptography.X509Certificates;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Security.Signing;
using Eu.EDelivery.AS4.Services.Journal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Send;

/// <summary>
/// Describes how the MSH signs the AS4 UserMessage
/// </summary>
[Info("Sign the AS4 Message if necessary")]
[Description("This step signs the AS4 Message if signing is enabled in the Sending PMode")]
public class SignAS4MessageStep : IStep
{
    private readonly ILogger<SignAS4MessageStep> _logger;
    private readonly ICertificateRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignAS4MessageStep"/> class. 
    /// Create Signing Step with a given Certificate Store Repository
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="repository">
    /// </param>
    public SignAS4MessageStep(ILogger<SignAS4MessageStep> logger, ICertificateRepository repository)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Sign the <see cref="AS4Message" />
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        if (messagingContext.AS4Message == null)
        {
            throw new InvalidOperationException(
                $"{nameof(SignAS4MessageStep)} requires an AS4Message to sign but no AS4Message is present in the MessagingContext");
        }

        if (messagingContext.AS4Message.IsEmpty)
        {
            _logger.LogDebug("No signing will be performed on the message because it's empty");
            return await StepResult.SuccessAsync(messagingContext);
        }

        var signInfo = RetrieveSigningInformation(
            messagingContext.AS4Message,
            messagingContext.SendingPMode,
            messagingContext.ReceivingPMode);

        if (signInfo == null)
        {
            _logger.LogTrace("No signing will be performend on the message because no signing information was found in either Sending or Receiving PMode");
            return await StepResult.SuccessAsync(messagingContext);
        }

        if (!signInfo.IsEnabled)
        {
            _logger.LogTrace("No signing will be performend on the message because the PMode siging information is disabled");
            return await StepResult.SuccessAsync(messagingContext);
        }

        _logger.LogInformation("(Outbound)[{PrimaryMessageId}] Sign AS4Message with given signing information of the PMode",
            messagingContext.AS4Message.GetPrimaryMessageId());

        var certificate = RetrieveCertificate(signInfo);
        var settings =
            new CalculateSignatureConfig(
                signingCertificate: certificate,
                referenceTokenType: signInfo.KeyReferenceMethod,
                signingAlgorithm: signInfo.Algorithm,
                hashFunction: signInfo.HashFunction);

        SignAS4Message(settings, messagingContext.AS4Message);

        var entry = JournalLogEntry.CreateFrom(
            messagingContext.AS4Message,
            $"Signed with certificate {settings.SigningCertificate.FriendlyName} and reference {settings.ReferenceTokenType} "
            + $"using algorithm {settings.SigningAlgorithm} and hash {settings.HashFunction}");

        var result = await StepResult.SuccessAsync(messagingContext);
        _logger.LogTrace("Append log to message journal: {LogEntries}", string.Join(", ", entry.LogEntries));
        return await result.WithJournalAsync(entry);
    }

    private Signing? RetrieveSigningInformation(
        AS4Message message,
        SendingProcessingMode? sendingPMode,
        ReceivingProcessingMode? receivingPMode)
    {
        if (message.IsUserMessage || message.IsPullRequest)
        {
            if (sendingPMode == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(SignAS4MessageStep)} requires a SendingPMode when the primary message unit of the AS4Message is either an UserMessage or a PullRequest");
            }

            _logger.LogTrace("Use SendingPMode {PModeId} for signing because the primary message unit is a UserMessage or a PullRequest", sendingPMode.Id);
            return sendingPMode.Security?.Signing;
        }

        if (sendingPMode != null)
        {
            // When signal messages are forwarded, we have a sending pmode instead of a receiving pmode.
            return sendingPMode?.Security?.Signing;
        }

        if (message.PrimaryMessageUnit is Receipt)
        {
            if (receivingPMode == null)
            {
                throw new InvalidOperationException(
                $"{nameof(SignAS4MessageStep)} requires a ReceivingPMode when the primary message unit of the AS4Message is a Receipt");

            }

            _logger.LogTrace("Use ReceivingPMode {PModeId} for signing because the primary message unit of the AS4Message is a Receipt", receivingPMode.Id);
            return receivingPMode.ReplyHandling?.ResponseSigning;
        }

        if (message.PrimaryMessageUnit is Error)
        {
            if (receivingPMode == null)
            {
                // When the error occured before there was a ReceivingPMode determined, we can't retrieve any signing information.
                _logger.LogTrace("No ReceivingPMode was found for signing the AS4Message with an Error as primary message unit");
                return null;
            }

            _logger.LogTrace("Use ReceivingPMode {PModeId} for signing because the primary message unit of the AS4Message is an Error", receivingPMode.Id);
            return receivingPMode.ReplyHandling?.ResponseSigning;
        }

        throw new InvalidOperationException(
            "No signing information can be retrieved from both Sending and Receiving PMode based on the message type");
    }

    private void SignAS4Message(CalculateSignatureConfig settings, AS4Message message)
    {
        try
        {
            message.Sign(settings);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Sign AS4Message failed");
            throw;
        }
    }

    private X509Certificate2 RetrieveCertificate(Signing signInfo)
    {
        if (signInfo.SigningCertificateInformation == null)
        {
            throw new ConfigurationErrorsException(
                "No signing certificate information found in PMode to perform signing. "
                + "Please provide either a <CertificateFindCriteria/> or <PrivateKeyCertificate/> tag to the Security.Signing element");
        }

        if (signInfo.SigningCertificateInformation is CertificateFindCriteria certFindCriteria)
        {
            return _repository.GetCertificate(
                findType: certFindCriteria.CertificateFindType,
                privateKeyReference: certFindCriteria.CertificateFindValue);
        }

        if (signInfo.SigningCertificateInformation is PrivateKeyCertificate embeddedCertInfo && embeddedCertInfo.Certificate is not null)
        {
            return new X509Certificate2(
                rawData: Convert.FromBase64String(embeddedCertInfo.Certificate),
                password: embeddedCertInfo.Password,
                keyStorageFlags:
                    X509KeyStorageFlags.Exportable
                    | X509KeyStorageFlags.MachineKeySet
                    | X509KeyStorageFlags.PersistKeySet);
        }

        throw new NotSupportedException(
            "The signing certificate information specified in the PMode could not be used to retrieve the certificate. " +
            "Please provide either a <CertificateFindCriteria/> or <PrivateKeyCertificate/> tag to the Security.Signing element");
    }
}
