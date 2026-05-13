using System.ComponentModel;
using System.Security.Cryptography;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Security.Signing;
using Eu.EDelivery.AS4.Services;
using Eu.EDelivery.AS4.Services.Journal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Receive;

/// <summary>
/// Describes how a <see cref="AS4Message"/> signature gets verified
/// </summary>
[Info("Verify signature of received AS4 Message")]
[Description(
    "Verifies if the signature of the AS4 Message is correct. " +
    "Message verification is necessary to ensure that the authenticity of the message is intact.")]
public class VerifySignatureAS4MessageStep : IStep
{
    private readonly ILogger<VerifySignatureAS4MessageStep> _logger;
    private readonly ICertificateRepository _certificateRepository;
    private readonly IOutMessageService _outMessageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VerifySignatureAS4MessageStep" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="certificateRepository"></param>
    /// <param name="outMessageService"></param>
    public VerifySignatureAS4MessageStep(
        ILogger<VerifySignatureAS4MessageStep> logger,
        ICertificateRepository certificateRepository,
        IOutMessageService outMessageService)
    {
        _logger = logger;
        _certificateRepository = certificateRepository;
        _outMessageService = outMessageService;
    }

    /// <summary>
    /// Start verifying the Signature of the <see cref="AS4Message"/>
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        var as4Message = messagingContext.AS4Message
            ?? throw new InvalidOperationException(
                $"{nameof(VerifySignatureAS4MessageStep)} requires an AS4Message to verify but no AS4Message is present in the MessagingContext");

        var verification = DetermineSigningVerification(messagingContext);
        if (verification == null)
        {
            _logger.LogDebug("No PMode.Security.SigningVerification element found, so no signature verification will take place");
            return await StepResult.SuccessAsync(messagingContext);
        }

        (var unsignedButRequired, var desRequired) = SigningRequiredRule(verification, as4Message);
        if (unsignedButRequired)
        {
            return InvalidSignatureResult(
                desRequired, ErrorAlias.PolicyNonCompliance, messagingContext);
        }

        (var signedMessageButNotAllowed, var desNotAllowed) = SigningNotAllowedRule(verification, as4Message);
        if (signedMessageButNotAllowed)
        {
            return InvalidSignatureResult(desNotAllowed, ErrorAlias.PolicyNonCompliance, messagingContext);
        }

        if (!as4Message.IsSigned)
        {
            _logger.LogTrace("Signature will not be verified since the message is not signed");
            return await StepResult.SuccessAsync(messagingContext);
        }

        if (verification.Signature == Limit.Ignored)
        {
            _logger.LogDebug("Signature will not be verified because the PMode states that Security.SigningVerification=Ignored");
            return await StepResult.SuccessAsync(messagingContext);
        }

        if (as4Message.MessageUnits.Any(u => u is Receipt) &&
            (messagingContext.SendingPMode?.ReceiptHandling?.VerifyNRR ?? false))
        {
            if (!await VerifyNonRepudiationHashesAsync(as4Message, cancellation))
            {
                _logger.LogError("{LogTag} Incoming Receipt hasn't got valid NRI References", messagingContext.LogTag);

                return InvalidSignatureResult(
                    "The digest value in the Signature References of the referenced UserMessage " +
                    "doesn't match the References of the NRI of the incoming Non-Repudiation Receipt",
                    ErrorAlias.FailedAuthentication,
                    messagingContext);
            }

            _logger.LogDebug("{LogTag} Incoming Receipt has valid NRI References", messagingContext.LogTag);
        }

        return await TryVerifyingSignatureAsync(messagingContext, verification);
    }

    private SigningVerification? DetermineSigningVerification(MessagingContext ctx)
    {
        if (ctx.AS4Message!.IsSignalMessage && !ctx.AS4Message.IsMultiHopMessage)
        {
            _logger.LogTrace("Use SendingPMode {PModeId} for signature verification", ctx.SendingPMode?.Id);
            return ctx.SendingPMode?.Security?.SigningVerification;
        }

        _logger.LogTrace("Use ReceivingPMode {PModeId} for signature verification", ctx.ReceivingPMode?.Id);
        return ctx.ReceivingPMode?.Security?.SigningVerification;
    }

    private static (bool, string) SigningRequiredRule(SigningVerification v, AS4Message m)
    {
        return (v.Signature == Limit.Required && !m.IsSigned,
                "PMode requires a signed AS4Message but the received AS4message is not signed");
    }

    private static (bool, string) SigningNotAllowedRule(SigningVerification v, AS4Message m)
    {
        return (v.Signature == Limit.NotAllowed && m.IsSigned,
                "PMode doesn't allow a signed AS4Message and the received AS4Message is signed");
    }

    private async Task<bool> VerifyNonRepudiationHashesAsync(AS4Message as4Message, CancellationToken cancellation)
    {
        var receipts = as4Message.SignalMessages
            .Where(m => m is Receipt r && r.NonRepudiationInformation is not null)
            .Cast<Receipt>();

        var userMessages =
            (await GetReferencedUserMessagesAsync(receipts, cancellation)).Where(m => m != null && m.IsSigned);

        if (!userMessages.Any())
        {
            _logger.LogDebug(
                "Non-Repudiation references of the Receipt(s) can't be verified because no UserMessage(s) are found for the incoming Receipt(s)");
        }

        return receipts.All(nrrReceipt =>
        {
            var refUserMessage = userMessages.FirstOrDefault(
                u => u.GetPrimaryMessageId() == nrrReceipt.RefToMessageId);

            return refUserMessage == null
                   || nrrReceipt.VerifyNonRepudiationInfo(refUserMessage);
        });
    }

    private async Task<IEnumerable<AS4Message>> GetReferencedUserMessagesAsync(IEnumerable<Receipt> receipts, CancellationToken cancellation)
    {
        return await _outMessageService.GetNonIntermediaryAS4UserMessagesForIds(receipts
            .Where(r => r.RefToMessageId != null)
            .Select(r => r.RefToMessageId!),
            cancellation);
    }

    private async Task<StepResult> TryVerifyingSignatureAsync(
        MessagingContext messagingContext,
        SigningVerification verification)
    {
        ArgumentNullException.ThrowIfNull(messagingContext.AS4Message);

        try
        {
            var options = CreateVerifyOptionsForAS4Message(messagingContext.AS4Message, verification);

            _logger.LogDebug("Verify signature on the AS4Message {{AllowUnknownRootCertificateAuthority={AllowUnknownRootCertificateAuthority}}}",
                options.AllowUnknownRootCertificateAuthority);
            if (!messagingContext.AS4Message.VerifySignature(options))
            {
                return InvalidSignatureResult(
                    "The signature is invalid",
                    ErrorAlias.FailedAuthentication,
                    messagingContext);
            }

            _logger.LogInformation("{LogTag} AS4Message has a valid signature present", messagingContext.LogTag);

            var entry =
                JournalLogEntry.CreateFrom(
                    messagingContext.AS4Message,
                    $"Signature verified with {(options.AllowUnknownRootCertificateAuthority ? "allowing" : "disallowing")} unknown certificate authorities");

            var result = await StepResult.SuccessAsync(messagingContext);
            _logger.LogTrace("Append log to message journal: {LogEntries}", string.Join(", ", entry.LogEntries));
            return await result.WithJournalAsync(entry);
        }
        catch (CryptographicException exception)
        {
            var description = "Signature verification failed";

            if (messagingContext.AS4Message.IsEncrypted)
            {
                _logger.LogError(
                    "Signature verification failed because the received message is still encrypted. "
                    + "Make sure that you specify <Decryption/> information in the <Security/> element of the "
                    + "ReceivingPMode so the ebMS MessagingHeader is first decrypted before it's signature gets verified");

                description = "Signature verification failed because the message is still encrypted";
            }

            _logger.LogError(exception, "{LogTag} An exception occured while validating the signature", messagingContext.LogTag);
            return InvalidSignatureResult(
                description,
                ErrorAlias.FailedAuthentication,
                messagingContext);
        }
    }

    private VerifySignatureConfig CreateVerifyOptionsForAS4Message(AS4Message as4Message, SigningVerification v)
    {
        return new VerifySignatureConfig(
            v.AllowUnknownRootCertificate,
            v.AllowExpiredCertificate,
            as4Message.Attachments,
            _certificateRepository);
    }

    private StepResult InvalidSignatureResult(string description, ErrorAlias errorAlias, MessagingContext context)
    {
        _logger.LogError(description);

        context.ErrorResult = new ErrorResult(description, errorAlias);
        return StepResult.Failed(context);
    }
}
