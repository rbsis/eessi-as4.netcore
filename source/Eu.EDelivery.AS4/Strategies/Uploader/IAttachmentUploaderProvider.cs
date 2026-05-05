namespace Eu.EDelivery.AS4.Strategies.Uploader;

public interface IAttachmentUploaderProvider
{
    /// <summary>
    /// Accept a <see cref="IAttachmentUploader" /> implementation in the <see cref="IAttachmentUploaderProvider" />.
    /// </summary>
    /// <param name="condition">Condition for which the <see cref="IAttachmentUploader" /> must be used.</param>
    /// <param name="uploader"><see cref="IAttachmentUploader" /> implementation to be used.</param>
    void Accept(Func<string, bool> condition, IAttachmentUploader uploader);

    /// <summary>
    /// Get a <see cref="IAttachmentUploader" /> implementation based on a given <paramref name="type" />.
    /// </summary>
    /// <param name="type">The type for which the <see cref="IAttachmentUploader" /> implementation is accepted.</param>
    /// <returns></returns>
    IAttachmentUploader Get(string type);
}
