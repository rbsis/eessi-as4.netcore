using System.ComponentModel;
using Eu.EDelivery.AS4.Compression;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Services.Journal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Receive;

/// <summary>
/// Decompress the incoming Payloads
/// </summary>
[Info("Decompress attachments")]
[Description("If necessary, decompresses the attachments that are present in the received message.")]
public class DecompressAttachmentsStep : IStep
{
    private readonly ILogger<DecompressAttachmentsStep> _logger;
    private readonly ICompressStrategy _compressStrategy;

    public DecompressAttachmentsStep(ILogger<DecompressAttachmentsStep> logger, ICompressStrategy compressStrategy)
    {
        _logger = logger;
        _compressStrategy = compressStrategy;
    }

    /// <summary>
    /// Decompress any Attachments
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
                $"{nameof(DecompressAttachmentsStep)} requires a AS4Message but no AS4Message is present in the MessagingContext");
        }

        if (messagingContext.ReceivedMessageMustBeForwarded)
        {
            _logger.LogDebug("No decompression will happen because the incoming AS4Message must be forwarded unchanged");
            return await StepResult.SuccessAsync(messagingContext);
        }

        if (!messagingContext.AS4Message.HasAttachments)
        {
            _logger.LogDebug("No decompression will happen because the AS4Message hasn't got any attachments to decompress");
            return await StepResult.SuccessAsync(messagingContext);
        }

        if (messagingContext.AS4Message.IsEncrypted)
        {
            _logger.LogWarning("Incoming attachmets are still encrypted will fail to decompress correctly");
        }

        try
        {
            _compressStrategy.DecompressAttachments(messagingContext.AS4Message);

            var entry = JournalLogEntry.CreateFrom(
                messagingContext.AS4Message,
                $"Decompressed {messagingContext.AS4Message.Attachments.Count()} with GZip compression");

            var result = await StepResult.SuccessAsync(messagingContext);
            _logger.LogTrace("Append log to message journal: {LogEntries}", string.Join(", ", entry.LogEntries));
            return await result.WithJournalAsync(entry);
        }
        catch (Exception exception)
        when (exception is ArgumentException
              || exception is ObjectDisposedException
              || exception is InvalidDataException)
        {
            var description = "Decompression failed due to an exception";

            if (messagingContext.AS4Message.IsEncrypted)
            {
                _logger.LogError(exception,
                    "Decompression failed because the incoming attachments are still encrypted. "
                    + "Make sure that you specify <Decryption/> information in the <Security/> element of the "
                    + "ReceivingPMode so the attachments are first decrypted before decompressed");

                description = "Decompression failed because the incoming attachments are still encrypted";
            }

            messagingContext.ErrorResult = new ErrorResult(
                description,
                ErrorAlias.DecompressionFailure);

            return await StepResult.FailedAsync(messagingContext);
        }
    }
}
