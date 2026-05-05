using System.Net;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Http.Response;

/// <summary>
/// Contract to define the HTTP/AS4 response being handled.
/// </summary>
public interface IAS4Response
{
    /// <summary>
    /// Gets the HTTP Status Code of the HTTP response.
    /// </summary>
    HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Gets the an AS4Message representation of the response.
    /// </summary>
    AS4Message ReceivedAS4Message { get; }

    /// <summary>
    /// Gets a Stream that contains the response like it has been received.
    /// </summary>
    ReceivedMessage ReceivedStream { get; }

    /// <summary>
    /// Gets the Original Request from this response.
    /// </summary>
    MessagingContext OriginalRequest { get; }
}
