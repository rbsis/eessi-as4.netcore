using System.IO.Compression;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Streaming;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Compression;

internal class CompressStrategy : ICompressStrategy
{
    public const string CompressionType = "application/gzip";

    private readonly ILogger<CompressStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompressStrategy"/> class.
    /// </summary>
    public CompressStrategy(ILogger<CompressStrategy> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Compresses the Attachments that are part of this AS4 Message and
    /// modifies the Payload-info in the UserMessage to indicate that the attachment 
    /// is compressed.
    /// </summary>
    public void CompressAttachments(AS4Message message)
    {
        if (message.UserMessages.Any(u => u is null))
        {
            throw new ArgumentNullException(nameof(message), @"AS4Message.UserMessages. contains a 'null' instance");
        }

        Compress(message.UserMessages.SelectMany(u => u.PayloadInfo), message.Attachments);

        // Since the headers in the message have changed, the EnvelopeDocument
        // is no longer in sync and should be set to null.
        message.EnvelopeDocument = null;
    }

    private void Compress(IEnumerable<PartInfo> payloadInfo, IEnumerable<Attachment> attachments)
    {
        if (!attachments.Any())
        {
            _logger.LogDebug("No attachments present in AS4Message to compress");
            return;
        }

        if (attachments.Any(a => a is null))
        {
            throw new ArgumentNullException(nameof(attachments), @"AS4Message.Attachments contains a 'null' instance");
        }

        if (payloadInfo.Any(p => p is null))
        {
            throw new ArgumentNullException(nameof(payloadInfo), @"AS4Message.UserMessage.PartInfos contains a 'null' instance");
        }

        foreach (var attachment in attachments)
        {
            var partInfo = payloadInfo.FirstOrDefault(attachment.Matches)
                ?? throw new InvalidOperationException($"Can't compress attachment {attachment.Id} because no matching PartInfo element was found in UserMessage");

            CompressAttachment(partInfo, attachment);
        }
    }

    private static void CompressAttachment(PartInfo partInfo, Attachment attachment)
    {
        var outputStream = VirtualStream.Create(
            attachment.EstimatedContentSize > -1
                ? attachment.EstimatedContentSize
                : VirtualStream.ThresholdMax);

        var compressionLevel = DetermineCompressionLevelFor(attachment);

        using (var gzipCompression = new GZipStream(outputStream, compressionLevel, leaveOpen: true))
        {
            attachment.Content.CopyTo(gzipCompression);
        }

        outputStream.Position = 0;
        attachment.MimeType = attachment.ContentType;
        attachment.CompressionType = CompressionType;
        partInfo.CompressionType = CompressionType;
        attachment.UpdateContent(outputStream, CompressionType);
    }

    private static CompressionLevel DetermineCompressionLevelFor(Attachment attachment)
    {
        if (attachment.ContentType.Equals(CompressionType, StringComparison.OrdinalIgnoreCase))
        {
            // In certain cases, we do not want to waste time compressing the attachment, since
            // compressing will only take time without notably decreasing the attachment size.
            return CompressionLevel.Fastest;
        }

        if (attachment.EstimatedContentSize > -1)
        {
            const long TwelveKilobytes = 12_288;
            const long TwoHundredMegabytes = 209_715_200;

            if (attachment.EstimatedContentSize <= TwelveKilobytes ||
                attachment.EstimatedContentSize > TwoHundredMegabytes)
            {
                return CompressionLevel.Fastest;
            }
        }

        return CompressionLevel.Optimal;
    }

    /// <summary>
    /// Decompresses the Attachments that are part of this AS4 Message.
    /// </summary>
    public void DecompressAttachments(AS4Message message)
    {
        if (message.UserMessages.Any(u => u is null))
        {
            throw new ArgumentNullException(nameof(message), @"AS4Message.UserMessages. contains a 'null' instance");
        }

        Decompress(message.UserMessages.SelectMany(u => u.PayloadInfo), message.Attachments);
    }

    private void Decompress(IEnumerable<PartInfo> payloadInfo, IEnumerable<Attachment> attachments)
    {
        if (!attachments.Any())
        {
            _logger.LogDebug("No attachments present in AS4Message to decompress");
            return;
        }

        if (attachments.Any(a => a is null))
        {
            throw new ArgumentNullException(nameof(attachments), @"AS4Message.Attachments contains a 'null' instance");
        }

        if (payloadInfo.Any(p => p is null))
        {
            throw new ArgumentNullException(nameof(payloadInfo), @"AS4Message.UserMessage.PartInfos contains a 'null' instance");
        }

        foreach (var attachment in attachments)
        {
            if (!attachment.IsCompressed)
            {
                _logger.LogDebug("Skip Attachment {AttachmentId} because it's not compressed", attachment.Id);
                continue;
            }

            if (!attachment.HasMimeType)
            {
                throw new InvalidDataException(
                    $"Cannot decompress attachment \"{attachment.Id}\" because it hasn't got a PartProperty called \"MimeType\"");
            }

            var partInfo = payloadInfo.FirstOrDefault(attachment.Matches)
                ?? throw new InvalidDataException($"Can't decompress Attachment {attachment.Id} because no matching PartInfo was found");

            _logger.LogTrace("Attachment {AttachmentId} will be decompressed", attachment.Id);
            DecompressAttachment(partInfo, attachment);
            _logger.LogDebug("Attachment {AttachmentId} is decompressed to a type of {ContentType}",
                attachment.Id,
                attachment.ContentType);
        }
    }

    private static void DecompressAttachment(PartInfo partInfo, Attachment attachment)
    {
        if (!partInfo.HasMimeType)
        {
            throw new InvalidDataException(
                $"Cannot decompress attachment {attachment.Id}: MimeType is not specified in referenced <PartInfo/> element");
        }

        if (partInfo.MimeType.IndexOf("/", StringComparison.OrdinalIgnoreCase) < 0)
        {
            throw new InvalidDataException(
                $"Cannot decompress attachment {attachment.Id}: Invalid MimeType {partInfo.MimeType} in referenced <PartInfo/> element");
        }

        attachment.ResetContentPosition();

        var decompressed = DecompressStream(attachment.Content);

        partInfo.CompressionType = CompressionType;
        attachment.CompressionType = CompressionType;
        attachment.MimeType = partInfo.MimeType;
        attachment.UpdateContent(decompressed, partInfo.MimeType);
    }

    private static VirtualStream DecompressStream(Stream input)
    {
        var outputStream = VirtualStream.Create();

        using var gzipCompression = new GZipStream(input, CompressionMode.Decompress, true);
        gzipCompression.CopyTo(outputStream);
        outputStream.Position = 0;

        return outputStream;
    }
}
