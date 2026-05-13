using Microsoft.Extensions.DependencyInjection;

namespace Eu.EDelivery.AS4.Strategies.Uploader;

/// <summary>
/// Class to provide the right <see cref="IAttachmentUploader" /> implementation
/// </summary>
internal class AttachmentUploaderProvider : IAttachmentUploaderProvider
{
    private readonly ICollection<UploaderEntry> _uploaders;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentUploaderProvider" /> class
    /// </summary>
    public AttachmentUploaderProvider(
        [FromKeyedServices(FileAttachmentUploader.Key)] IAttachmentUploader fileAttachmentUploader,
        [FromKeyedServices(EmailAttachmentUploader.Key)] IAttachmentUploader emailAttachmentUploader,
        [FromKeyedServices(PayloadServiceAttachmentUploader.Key)] IAttachmentUploader payloadServiceAttachmentUploader)
    {
        _uploaders = [];

        Accept(s => StringComparer.OrdinalIgnoreCase.Equals(s, FileAttachmentUploader.Key), fileAttachmentUploader);
        Accept(s => StringComparer.OrdinalIgnoreCase.Equals(s, EmailAttachmentUploader.Key), emailAttachmentUploader);
        Accept(s => StringComparer.OrdinalIgnoreCase.Equals(s, PayloadServiceAttachmentUploader.Key), payloadServiceAttachmentUploader);
    }

    /// <summary>
    /// Get the right <see cref="IAttachmentUploader" /> implementation
    /// for a given <paramref name="type" />
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public IAttachmentUploader Get(string type)
    {
        var entry = _uploaders.FirstOrDefault(u => u.Condition(type));

        if (entry?.Uploader == null)
        {
            throw new KeyNotFoundException(
                $"(Deliver) No {nameof(IAttachmentUploader)} impelemtation found for key: {type}");
        }

        return entry.Uploader;
    }

    /// <summary>
    /// Adds a new <see cref="IAttachmentUploader" /> implementation
    /// for a given <paramref name="condition" />
    /// </summary>
    /// <param name="condition"></param>
    /// <param name="uploader"></param>
    public void Accept(Func<string, bool> condition, IAttachmentUploader uploader) =>
        _uploaders.Add(new UploaderEntry(condition, uploader));

    private sealed class UploaderEntry
    {
        public UploaderEntry(Func<string, bool> condition, IAttachmentUploader uploader)
        {
            Condition = condition;
            Uploader = uploader;
        }

        public Func<string, bool> Condition { get; }

        public IAttachmentUploader Uploader { get; }
    }
}
