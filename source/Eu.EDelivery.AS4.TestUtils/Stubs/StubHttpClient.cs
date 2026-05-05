using System.Net;
using Eu.EDelivery.AS4.Http;
using Eu.EDelivery.AS4.Http.Response;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.Notify;
using Eu.EDelivery.AS4.Strategies.Sender;
using Moq;

namespace Eu.EDelivery.AS4.TestUtils.Stubs;

/// <summary>
/// <see cref="IReliableHttpClient" /> implementation to return a <see cref="AS4Message" />.
/// </summary>
public class StubHttpClient : IReliableHttpClient, ISenderHttpClient
{
    private readonly AS4Message? _expectedMessage;
    private readonly HttpStatusCode _expectedStatusCode;

    private readonly Exception? _exceptionToBeThrown;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubHttpClient" /> class.
    /// </summary>
    /// <param name="expectedMessage">The Expected <see cref="AS4Message" />.</param>
    /// <param name="expectedStatusCode">The expected status code.</param>
    private StubHttpClient(AS4Message expectedMessage, HttpStatusCode expectedStatusCode = HttpStatusCode.OK) : this(expectedStatusCode)
    {
        _expectedMessage = expectedMessage;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StubHttpClient"/> class.
    /// </summary>
    /// <param name="expectedStatusCode">The expected status code.</param>
    private StubHttpClient(HttpStatusCode expectedStatusCode)
    {
        _expectedStatusCode = expectedStatusCode;
    }

    private StubHttpClient(Exception exception)
    {
        _exceptionToBeThrown = exception;
    }

    public bool IsCalled { get; private set; }

    /// <summary>
    /// Creates a <see cref="StubHttpClient"/> that returns an empty response with a given status code
    /// </summary>
    /// <param name="statusCode"></param>
    /// <returns></returns>
    public static StubHttpClient ThatReturns(HttpStatusCode statusCode) => new(statusCode);

    /// <summary>
    /// Creates a <see cref="StubHttpClient"/> that returns a filled body with 
    /// </summary>
    /// <param name="as4Message"></param>
    /// <param name="statusCode"></param>
    /// <returns></returns>
    public static IReliableHttpClient ThatReturns(AS4Message as4Message, HttpStatusCode statusCode = HttpStatusCode.OK) => new StubHttpClient(as4Message, statusCode);

    public static IReliableHttpClient ThatThrows(Exception exception) => new StubHttpClient(exception);

    /// <summary>
    /// Request a Message for the <see cref="IReliableHttpClient"/> implementation.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="contentType"></param>
    /// <returns></returns>
    public IHttpRequest CreateRequest(string url, string contentType)
    {
        var request = new Mock<IHttpRequest>();

        return request.Object;
    }

    public Task<IAS4Response> PostRequestAsync(IHttpRequest request, MessagingContext ctx, CancellationToken cancellation)
    {
        IsCalled = true;

        if (_exceptionToBeThrown != null)
        {
            throw _exceptionToBeThrown;
        }

        var response = new Mock<IAS4Response>();
        response.Setup(r => r.StatusCode).Returns(_expectedStatusCode);
        response.Setup(r => r.OriginalRequest).Returns(ctx);
        response.Setup(r => r.ReceivedAS4Message).Returns(_expectedMessage ?? AS4Message.Empty);

        return Task.FromResult(response.Object);
    }

    public Task<HttpStatusCode> PostDeliverMessageEnvelopeAsync(string url, DeliverMessageEnvelope envelop, CancellationToken cancellation)
    {
        IsCalled = true;

        if (_exceptionToBeThrown != null)
        {
            throw _exceptionToBeThrown;
        }

        return Task.FromResult(_expectedStatusCode);
    }

    public Task<HttpStatusCode> PostNotifyMessageEnvelopeAsync(string url, NotifyMessageEnvelope envelop, CancellationToken cancellation)
    {
        IsCalled = true;

        if (_exceptionToBeThrown != null)
        {
            throw _exceptionToBeThrown;
        }

        return Task.FromResult(_expectedStatusCode);
    }
}
