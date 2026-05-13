using System.ComponentModel;
using System.IO.Compression;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Streaming;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Eu.EDelivery.AS4.Steps.Deliver;

/// <summary>
/// <see cref="IStep"/> implementation to .zip the attachments to one file
/// </summary>
[Info("Zip payloads in one archive")]
[Description("If the received AS4 Message contains multiple attachments, then this step zips them into one payload.")]
public class ZipAttachmentsStep : IStep
{
    private readonly ILogger<ZipAttachmentsStep> _logger;

    public ZipAttachmentsStep(ILogger<ZipAttachmentsStep> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Start zipping <see cref="Attachment"/> Models
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        if (messagingContext?.AS4Message == null)
        {
            throw new InvalidOperationException(
                $"{nameof(ZipAttachmentsStep)} requires an AS4Message to zip the attachments but no AS4Message is present in the MessagingContext");
        }

        if (messagingContext.AS4Message.Attachments.Count() > 1)
        {
            var zippedStream = await ZipAttachmentsInAS4MessageAsync(messagingContext.AS4Message, cancellation);
            var zipAttachment = CreateZippedAttachment(zippedStream);

            OverwriteAttachmentEntries(messagingContext.AS4Message, zipAttachment);
        }

        _logger.LogInformation("{LogTag} Zip the Attachments to a single file", messagingContext.LogTag);
        return await StepResult.SuccessAsync(messagingContext);
    }

    private static async Task<Stream> ZipAttachmentsInAS4MessageAsync(AS4Message message, CancellationToken cancellation)
    {
        var stream = new VirtualStream(forAsync: true);

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var attachment in message.Attachments)
            {
                var archiveEntry = CreateAttachmentEntry(archive, attachment);
                await AddAttachmentStreamToEntryAsync(attachment.Content, archiveEntry, cancellation);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static ZipArchiveEntry CreateAttachmentEntry(ZipArchive archive, Attachment attachment)
    {
        _ = MimeTypes.TryGetExtension(attachment.ContentType, out var extension);
        var entryName = attachment.Id + extension;
        return archive.CreateEntry(entryName, CompressionLevel.Optimal);
    }

    private static async Task AddAttachmentStreamToEntryAsync(Stream attachmentStream, ZipArchiveEntry entry, CancellationToken cancellation)
    {
        using var entryStream = entry.Open();
        await attachmentStream.CopyToAsync(entryStream, cancellation);
    }

    private static Attachment CreateZippedAttachment(Stream stream)
    {
        return new Attachment(
            id: Guid.NewGuid().ToString(),
            content: stream,
            contentType: "application/zip");
    }

    private static void OverwriteAttachmentEntries(AS4Message message, Attachment zipAttachment)
    {
        message.RemoveAllAttachments();
        message.AddAttachment(zipAttachment);
    }
}
