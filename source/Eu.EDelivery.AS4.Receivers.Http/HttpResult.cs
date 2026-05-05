using System.Net;
using Eu.EDelivery.AS4.Receivers.Http.Get;
using Eu.EDelivery.AS4.Receivers.Http.Post;
using Eu.EDelivery.AS4.Streaming;

namespace Eu.EDelivery.AS4.Receivers.Http;

/// <summary>
/// Result of the <see cref="Router"/> of the request through the <see cref="IHttpGetHandler"/>s and <see cref="IHttpPostHandler"/>s.
/// </summary>
public class HttpResult
{
    private readonly HttpStatusCode _status;
    private readonly string _contentType;
    private readonly Func<HttpListenerResponse, Task> _writeToAsync;

    internal HttpResult(
        HttpStatusCode status,
        string contentType,
        Func<HttpListenerResponse, Task> writeToAsync)
    {
        _status = status;
        _contentType = contentType;
        _writeToAsync = writeToAsync;
    }

    /// <summary>
    /// Creates a new empty result with only a status code.
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    public static HttpResult Empty(HttpStatusCode status) => FromBytes(status, [], string.Empty);

    /// <summary>
    /// Creates a new empty result with status and content type.
    /// </summary>
    /// <param name="status"></param>
    /// <param name="contentType"></param>
    /// <returns></returns>
    public static HttpResult Empty(HttpStatusCode status, string contentType) => FromBytes(status, [], contentType);

    /// <summary>
    /// Creates a new result from a series of bytes.
    /// </summary>
    /// <param name="status"></param>
    /// <param name="content"></param>
    /// <param name="contentType"></param>
    /// <returns></returns>
    public static HttpResult FromBytes(HttpStatusCode status, byte[] content, string contentType) => new(
        status,
        contentType,
        async response =>
        {
            response.ContentLength64 = content.Length;
            await response.OutputStream.WriteAsync(content);
        });

    /// <summary>
    /// Creates a new result from a stream.
    /// </summary>
    /// <param name="status"></param>
    /// <param name="content"></param>
    /// <param name="contentType"></param>
    /// <returns></returns>
    public static HttpResult FromStream(HttpStatusCode status, Stream content, string contentType) => new(
        status,
        contentType,
        async response =>
        {
            content.MovePositionToStreamStart();
            await content.CopyToAsync(response.OutputStream);
        });


    /// <summary>
    /// Write the configured contents to the specified HTTP response.
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    public async Task WriteToAsync(HttpListenerResponse response)
    {
        response.StatusCode = (int)_status;
        response.ContentType = _contentType;
        response.KeepAlive = false;

        await _writeToAsync(response);
    }
}
