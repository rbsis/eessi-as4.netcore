using Newtonsoft.Json.Linq;

namespace Eu.EDelivery.AS4.Fe.SubmitTool;

/// <summary>
/// Implementation to upload payloads to a payload service
/// </summary>
/// <seealso cref="IPayloadHandler" />
public class PayloadHttpServiceHandler : IPayloadHandler
{
    /// <summary>
    /// Determines whether this instance can handle the specified location.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns>
    ///   <c>true</c> if this instance can handle the specified location; otherwise, <c>false</c>.
    /// </returns>
    public bool CanHandle(string location) => location.StartsWith("http://", StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    /// Handles the specified location.
    /// </summary>
    /// <param name="location">The location to send to payload to.</param>
    /// <param name="fileName"></param>
    /// <param name="stream">The stream containing the payload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>String containing the download url to be used in the message.</returns>
    public async Task<string?> HandleAsync(string location, string fileName, Stream stream, CancellationToken cancellationToken)
    {
        // Http upload
        using var client = new HttpClient();
        var form = new MultipartFormDataContent();
        var content = new StreamContent(stream);
        form.Add(content);
        var response = await client.PostAsync(location, form, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        return JObject.Parse(result)["downloadUrl"]?.Value<string>();
    }
}
