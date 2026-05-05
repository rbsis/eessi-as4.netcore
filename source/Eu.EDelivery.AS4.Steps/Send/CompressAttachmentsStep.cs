using System.ComponentModel;
using Eu.EDelivery.AS4.Compression;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Services.Journal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Send;

/// <summary>
/// Describes how the attachments of an AS4 message must be compressed.
/// </summary>
[Info("Compress AS4 Message attachments if necessary")]
[Description("This step compresses the attachments of an AS4 Message if compression is enabled in the sending PMode.")]
public class CompressAttachmentsStep : IStep
{
    private readonly ILogger<CompressAttachmentsStep> _logger;
    private readonly ICompressStrategy _compressStrategy;

    public CompressAttachmentsStep(
        ILogger<CompressAttachmentsStep> logger,
        ICompressStrategy compressStrategy)
    {
        _logger = logger;
        _compressStrategy = compressStrategy;
    }

    /// <summary>
    /// Compress the <see cref="AS4Message" /> if required
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
                $"{nameof(CompressAttachmentsStep)} requires an AS4Message to compress it attachments but no AS4Message is present in the MessagingContext");
        }

        if (messagingContext.SendingPMode == null)
        {
            throw new InvalidOperationException(
                $"{nameof(CompressAttachmentsStep)} requires a Sending Processing Mode to use during the compression but no Sending Processing Mode is present in the MessagingContext");
        }

        if (messagingContext.SendingPMode.MessagePackaging?.UseAS4Compression == false)
        {
            _logger.LogTrace("No compression will happen because the SendingPMode {PModeId} MessagePackaging.UseAS4Compression is disabled", messagingContext.SendingPMode.Id);
            return await StepResult.SuccessAsync(messagingContext);
        }

        try
        {
            _logger.LogInformation("(Outbound)[{PrimaryMessageId}] Compress AS4Message attachments with GZip compression",
                messagingContext.AS4Message.GetPrimaryMessageId());
            _compressStrategy.CompressAttachments(messagingContext.AS4Message);
        }
        catch (SystemException exception)
        {
            const string Description = "Attachments cannot be compressed because of an exception";
            _logger.LogError(exception, Description);

            throw new InvalidDataException(Description, exception);
        }

        var entry =
            JournalLogEntry.CreateFrom(
                messagingContext.AS4Message,
                $"Compressed {messagingContext.AS4Message.Attachments.Count()} attachments with GZip compression");

        var result = await StepResult.SuccessAsync(messagingContext);
        _logger.LogTrace("Append log to message journal: {LogEntries}", string.Join(", ", entry.LogEntries));
        return await result.WithJournalAsync(entry);
    }
}
