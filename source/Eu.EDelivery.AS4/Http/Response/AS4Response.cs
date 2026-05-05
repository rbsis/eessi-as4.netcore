using System.Net;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Http.Response;

/// <summary>
/// 
/// </summary>
internal sealed class AS4Response : IAS4Response
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AS4Response" /> class.
    /// </summary>
    /// <param name="statusCode">The web Response.</param>
    /// <param name="requestMessage">The HTTP Status Code.</param>
    /// <param name="receivedStream"></param>
    /// <param name="receivedAS4Message"></param>
    internal AS4Response(
        HttpStatusCode statusCode,
        MessagingContext requestMessage,
        ReceivedMessage receivedStream,
        AS4Message receivedAS4Message)
    {
        StatusCode = statusCode;
        ReceivedStream = receivedStream;
        ReceivedAS4Message = receivedAS4Message;
        OriginalRequest = requestMessage;
    }

    /// <summary>
    /// Gets the HTTP Status Code of the HTTP response.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Gets the Message from the AS4 response.
    /// </summary>
    public AS4Message ReceivedAS4Message { get; }

    public ReceivedMessage ReceivedStream { get; }

    /// <summary>
    /// Gets the Original Request from this response.
    /// </summary>
    public MessagingContext OriginalRequest { get; }
}
