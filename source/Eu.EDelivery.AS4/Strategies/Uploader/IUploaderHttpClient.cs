using Eu.EDelivery.AS4.Model.Core;

namespace Eu.EDelivery.AS4.Strategies.Uploader;

public interface IUploaderHttpClient
{
    Task<UploadResult?> PostAttachmentAsync(string url, Attachment attachment, CancellationToken cancellation);
}
