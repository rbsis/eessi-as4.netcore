using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;

namespace Eu.EDelivery.AS4.Strategies.Uploader;

/// <summary>
/// <see cref="IAttachmentUploader" /> implementation to upload <see cref="Attachment" /> models as Multipart Form data.
/// </summary>
[Info(PayloadServiceAttachmentUploader.Key)]
public class PayloadServiceAttachmentUploader : IAttachmentUploader
{
    public const string Key = "PAYLOAD-SERVICE";

    private readonly IUploaderHttpClient _httpClient;

    [Info("location")]
    private string? Location { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadServiceAttachmentUploader" /> class.
    /// </summary>
    /// <param name="httpClient"></param>
    public PayloadServiceAttachmentUploader(IUploaderHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Configure the <see cref="IAttachmentUploader" />
    /// with a given <paramref name="payloadReferenceMethod" />
    /// </summary>
    /// <param name="payloadReferenceMethod"></param>
    public void Configure(Method payloadReferenceMethod)
    {
        var location = payloadReferenceMethod["location"]?.Value;
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new InvalidOperationException(
                $"{nameof(PayloadServiceAttachmentUploader)} requires a location to upload the attachments to, please add a "
                + "<Parameter key=\"location\" value=\"your-payload-service-endpoint\"/> to the MessageHandling.Deliver.PayloadReferenceMethod in the ReceivingPMode");
        }

        Location = location;
    }

    /// <inheritdoc />
    public async Task<UploadResult?> UploadAsync(Attachment attachment, MessageInfo referringUserMessage, CancellationToken cancellation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Location);

        return await _httpClient.PostAttachmentAsync(Location, attachment, cancellation);
    }
}
