using Eu.EDelivery.AS4.Http;
using Eu.EDelivery.AS4.Model.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Eu.EDelivery.AS4.Strategies.Uploader;

internal class UploaderHttpClient : HttpClientBase, IUploaderHttpClient
{
    public UploaderHttpClient(ILogger<UploaderHttpClient> logger) : base(logger)
    {
    }

    public async Task<UploadResult?> PostAttachmentAsync(string url, Attachment attachment, CancellationToken cancellation)
    {
        var form = new MultipartFormDataContent { { new StreamContent(attachment.Content), attachment.Id, attachment.Id } };

        using var response = await PostRequestAsync(url, form, null, cancellation);
        Logger.LogInformation("(Deliver) Upload attachment returns HTTP StatusCode: {StatusCode}", response.StatusCode);

        var serializedContent = await response.Content.ReadAsStringAsync(cancellation);
        return JsonConvert.DeserializeObject<UploadResult>(serializedContent);
    }
}
