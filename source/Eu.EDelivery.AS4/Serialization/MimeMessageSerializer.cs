using System.Text;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.IO;

namespace Eu.EDelivery.AS4.Serialization;

/// <summary>
/// Serialize <see cref="AS4Message" /> to a <see cref="Stream" />
/// </summary>
public class MimeMessageSerializer : ISerializer
{
    private readonly ILogger<MimeMessageSerializer> _logger;
    private readonly ISerializer _soapSerializer;

    private static readonly Lazy<FormatOptions> _formatOptions = new(() =>
    {
        var options = new FormatOptions();
        foreach (var headerId in Enum.GetValues(typeof(HeaderId)).Cast<HeaderId>())
        {
            options.HiddenHeaders.Add(headerId);
        }

        return options;
    }, LazyThreadSafetyMode.PublicationOnly);

    /// <summary>
    /// Initializes a new instance of the <see cref="MimeMessageSerializer"/> class. 
    /// Create a MIME Serializer of the <see cref="AS4Message"/>
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="soapSerializer">
    /// </param>
    public MimeMessageSerializer(
        ILogger<MimeMessageSerializer> logger,
        [FromKeyedServices(Constants.ContentTypes.Soap)] ISerializer soapSerializer)
    {
        _logger = logger;
        _soapSerializer = soapSerializer;
    }

    /// <summary>
    /// Asynchronously serializes the given <see cref="AS4Message"/> to a given <paramref name="output"/> stream.
    /// </summary>
    /// <param name="message">The message to serialize.</param>
    /// <param name="output">The destination stream to where the message should be written.</param>
    /// <param name="cancellation">The token to control the cancellation of the serialization.</param>
    public async Task SerializeAsync(
        AS4Message message,
        Stream output,
        CancellationToken cancellation)
    {
        try
        {
            await SerializeToMimeStreamAsync(message, output, cancellation);
        }
        catch (Exception exception)
        {
            throw new FormatException("An error occured while serializing the MIME message", exception);
        }
    }

    private async Task SerializeToMimeStreamAsync(AS4Message message, Stream stream, CancellationToken cancellationToken)
    {
        using var bodyPartStream = new MemoryStream(4096);
        await _soapSerializer.SerializeAsync(message, bodyPartStream, cancellationToken);

        var mimeMessage = CreateMimeMessage(message, bodyPartStream);
        foreach (var attachment in message.Attachments)
        {
            if (mimeMessage.Body is Multipart multipartBody)
            {
                AddAttachmentToMultipart(multipartBody, attachment);
            }
        }

        await mimeMessage.WriteToAsync(_formatOptions.Value, stream, cancellationToken);
    }

    /// <summary>
    /// Synchronously serializes the given <see cref="AS4Message"/> to a given <paramref name="output"/> stream.
    /// </summary>
    /// <param name="message">The message to serialize.</param>
    /// <param name="output">The destination stream to where the message should be written.</param>
    /// 
    public void Serialize(
        AS4Message message,
        Stream output)
    {
        try
        {
            SerializeToMimeStream(message, output);
        }
        catch (Exception exception)
        {
            throw new FormatException("An error occured while serializing the MIME message", exception);
        }
    }

    private void SerializeToMimeStream(AS4Message message, Stream stream)
    {
        using var bodyPartStream = new MemoryStream(4096);
        _soapSerializer.Serialize(message, bodyPartStream);
        var mimeMessage = CreateMimeMessage(message, bodyPartStream);

        foreach (var attachment in message.Attachments)
        {
            if (mimeMessage.Body is Multipart multipartBody)
            {
                AddAttachmentToMultipart(multipartBody, attachment);
            }
        }

        mimeMessage.WriteTo(_formatOptions.Value, stream);
    }

    private static MimeMessage CreateMimeMessage(AS4Message message, Stream bodyPartStream)
    {
        var contentId = $"_{Guid.NewGuid()}";
        var bodyPart = new MimePart("application", "soap+xml")
        {
            ContentId = contentId,
            Content = new MimeContent(bodyPartStream),
            ContentTransferEncoding = ContentEncoding.Binary
        };
        bodyPart.ContentType.Parameters["charset"] = Encoding.UTF8.HeaderName.ToLowerInvariant();

        var bodyMultipart = new Multipart("related") { bodyPart };

        ReassignContentType(bodyMultipart, message.ContentType);

        bodyMultipart.ContentType.Parameters["type"] = bodyPart.ContentType.MimeType;
        bodyMultipart.ContentType.Parameters["start"] = contentId;

        return new MimeMessage { Body = bodyMultipart };
    }

    private static void ReassignContentType(MimeEntity bodyMultipart, string type)
    {
        var contentType = ContentType.Parse(type);

        bodyMultipart.ContentType.Boundary = contentType.Boundary;
        bodyMultipart.ContentType.Charset = contentType.Charset;
        bodyMultipart.ContentType.Format = contentType.Format;
        bodyMultipart.ContentType.MediaSubtype = contentType.MediaSubtype;
        bodyMultipart.ContentType.MediaType = contentType.MediaType;
        bodyMultipart.ContentType.Name = contentType.Name;
        bodyMultipart.ContentType.Parameters.Clear();

        foreach (var item in contentType.Parameters)
        {
            bodyMultipart.ContentType.Parameters.Add(item);
        }
    }

    private void AddAttachmentToMultipart(Multipart bodyMultipart, Attachment attachment)
    {
        // A stream that is passed to a ContentObject must be seekable.  If this is not the case,
        // we'll have to create a new stream which is seekable and assign it to the Attachment.Content.
        if (!attachment.Content.CanSeek)
        {
            var tempStream = new VirtualStream(forAsync: true);
            attachment.Content.CopyTo(tempStream);
            tempStream.Position = 0;
            attachment.UpdateContent(tempStream, attachment.ContentType);
        }

        try
        {
            _ = MimeTypes.TryGetExtension(attachment.ContentType, out var extension);

            var attachmentMimePart = new MimePart(attachment.ContentType)
            {
                ContentId = attachment.Id,
                Content = new MimeContent(attachment.Content),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment)
                {
                    FileName = $"_{attachment.Id}{extension}"
                },
                ContentTransferEncoding = ContentEncoding.Binary
                // We need to explicitly set this to binary, 
                // otherwise we can enounter issues with CRLFs & signing.
            };
            bodyMultipart.Add(attachmentMimePart);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Attachment {Id} has a content-type that is not supported ({ContentType}).", attachment.Id, attachment.ContentType);
            throw new NotSupportedException($"Attachment {attachment.Id} has a content-type that is not supported ({attachment.ContentType}).");
        }
    }

    /// <summary>
    /// Asynchronously deserializes the given <paramref name="input"/> stream to an <see cref="AS4Message"/> model.
    /// </summary>
    /// <param name="input">The source stream from where the message should be read.</param>
    /// <param name="contentType">The content type required to correctly deserialize the message into different MIME parts.</param>
    /// <param name="cancellation">The token to control the cancellation of the deserialization.</param>
    public async Task<AS4Message> DeserializeAsync(
        Stream? input,
        string contentType,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrEmpty(contentType);

        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes($"Content-Type: {contentType}\r\n\r\n"));

        var chainedStream = new ChainedStream();
        chainedStream.Add(memoryStream, leaveOpen: false);
        chainedStream.Add(input, leaveOpen: true);

        try
        {
            return await ParseStreamToAS4MessageAsync(chainedStream, contentType, cancellation);
        }
        finally
        {
            // Since the stream has been read, make sure that it is re-positioned to the beginning.
            input.MovePositionToStreamStart();
        }
    }

    private async Task<AS4Message> ParseStreamToAS4MessageAsync(
        Stream inputStream,
        string contentType,
        CancellationToken cancellationToken)
    {
        var bodyParts = TryParseBodyParts(inputStream, cancellationToken);
        var envelopeStream = bodyParts[0].Content?.Open();

        var message = await _soapSerializer.DeserializeAsync(envelopeStream, contentType, cancellationToken);

        foreach (var userMessage in message.UserMessages)
        {
            var referencedPartInfos = userMessage.PayloadInfo ?? [];

            var attachments = BodyPartsAsAttachments(bodyParts, referencedPartInfos);
            message.AddAttachments(attachments);
        }

        return message;
    }

    private static List<MimePart> TryParseBodyParts(Stream inputStream, CancellationToken cancellationToken)
    {
        try
        {
            var mimeMessage = new MimeParser(inputStream, persistent: true).ParseMessage(cancellationToken);
            if (!mimeMessage.BodyParts.Any())
            {
                throw new FormatException("MIME Body Parts are empty");
            }

            return [.. mimeMessage.BodyParts.OfType<MimePart>()];
        }
        catch (Exception exception)
        {
            throw new InvalidMessageException(
                "The use of MIME is not consistent with the required usage in this specification", exception);
        }
    }

    private IEnumerable<Attachment> BodyPartsAsAttachments(
        IReadOnlyList<MimePart> bodyParts,
        IEnumerable<PartInfo> referencedPartInfos)
    {
        const int StartAfterSoapHeader = 1;
        for (var i = StartAfterSoapHeader; i < bodyParts.Count; i++)
        {
            var bodyPart = bodyParts[i];
            if (string.IsNullOrEmpty(bodyPart.ContentId) || bodyPart.Content == null)
            {
                continue;
            }

            var partInfo = referencedPartInfos.FirstOrDefault(i => i.Href.Contains(bodyPart.ContentId));
            if (partInfo is not null)
            {
                var stream = new VirtualStream();
                bodyPart.Content.DecodeTo(stream);
                stream.Position = 0;

                yield return new Attachment(
                    id: bodyPart.ContentId,
                    content: stream,
                    contentType: bodyPart.ContentType.MimeType,
                    props: partInfo.Properties.ToDictionary(kv => kv.Key, kv => kv.Value));
            }
            else
            {
                _logger.LogWarning("Attachment {ContentId} will be ignored because no matching <PartInfo /> is found", bodyPart.ContentId);
            }
        }
    }

}
