using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Serialization;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.TestUtils.Stubs;

public static class StubSender
{
    private static readonly HttpClient Client = new HttpClient();

    private static readonly Lazy<SoapEnvelopeSerializer> _lazySoapEnvelopeSerializer = new(() => new());

    private static readonly Lazy<MimeMessageSerializer> _lazyMimeMessageSerializer =
        new(() => new MimeMessageSerializer(NullLogger<MimeMessageSerializer>.Instance, _lazySoapEnvelopeSerializer.Value));

    private static readonly Lazy<SerializerProvider> _lazySerializerProvider =
        new(() => new SerializerProvider(_lazySoapEnvelopeSerializer.Value, _lazyMimeMessageSerializer.Value));


    /// <summary>
    /// Sends an AS4 Message to the endpoint that listens at the specified url.
    /// </summary>
    /// <param name="url">The url of the endpoint to send the message to.</param>
    /// <param name="as4Message">The AS4Message that must be sent.</param>
    /// <returns></returns>
    public static async Task<HttpResponseMessage> SendAS4Message(string url, AS4Message as4Message)
    {
        var request = await CreatePostRequestMessageAsync(url, as4Message, CancellationToken.None);

        Console.WriteLine($@"Send AS4Message as HTTP POST request to: {url}, Content-Type: {as4Message.ContentType}");
        return await Client.SendAsync(request);
    }

    /// <summary>
    /// Sends a request to the endpoint that listens at the specified url.
    /// </summary>
    /// <param name="url">The url of the endpoint to send the message to.</param>
    /// <param name="content">A byte array that contains the content of the request.</param>
    /// <param name="contentType">The contenttype.</param>
    /// <returns></returns>
    public static async Task<HttpResponseMessage> SendRequest(string url, byte[] content, string contentType)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new ByteArrayContent(content)
        };

        message.Content.Headers.Add("Content-Type", contentType);

        Console.WriteLine($@"Send HTTP POST request to: {url}, Content-Type: {contentType}");
        return await Client.SendAsync(message);
    }

    private static async Task<HttpRequestMessage> CreatePostRequestMessageAsync(string sendToUrl, AS4Message message, CancellationToken cancellation)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, sendToUrl);

        byte[] serializedMessage;

        using (var stream = new MemoryStream())
        {
            var serializer = _lazySerializerProvider.Value.Get(message.ContentType);
            await serializer.SerializeAsync(message, stream, cancellation);

            serializedMessage = stream.ToArray();
        }

        requestMessage.Content = new ByteArrayContent(serializedMessage);
        requestMessage.Content.Headers.Add("Content-Type", message.ContentType);

        return requestMessage;
    }
}
