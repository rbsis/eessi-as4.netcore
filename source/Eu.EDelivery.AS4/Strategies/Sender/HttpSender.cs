using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Notify;
using Eu.EDelivery.AS4.Model.PMode;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Strategies.Sender;

/// <summary>
/// <see cref="IDeliverSender"/>, <see cref="INotifySender"/> implemetation to HTTP POST on a configured endpoint.
/// </summary>
[Info(HttpSender.Key)]
public class HttpSender : IDeliverSender, INotifySender
{
    public const string Key = "HTTP";

    private readonly ILogger<HttpSender> _logger;
    private readonly ISenderHttpClient _httpClient;

    [Info("Destination URL", required: true)]
    private string Location { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpSender"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="client">HTTP client to handle the request/respond actions.</param>
    public HttpSender(ILogger<HttpSender> logger, ISenderHttpClient client)
    {
        _logger = logger;
        _httpClient = client;
        Location = string.Empty;
    }

    /// <summary>
    /// Configure the <see cref="IDeliverSender" />
    /// with a given <paramref name="method" />
    /// </summary>
    /// <param name="method"></param>
    public void Configure(Method method)
    {
        var location = method["location"]?.Value;
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new InvalidOperationException(
                $"{nameof(HttpSender)} requires a configured location to send the HTTP request to, please add a "
                + "<Parameter name=\"location\" value=\"your-http-endpoint\"/> it to the applicable "
                + $"Sending or ReceivingPMode for which the {nameof(HttpSender)} is configured");
        }

        Location = location;
    }

    /// <summary>
    /// Start sending the <see cref="DeliverMessage"/>
    /// </summary>
    /// <param name="deliverMessageEnvelope"></param>
    /// <param name="cancellation"></param>
    public async Task<SendResult> SendAsync(DeliverMessageEnvelope deliverMessageEnvelope, CancellationToken cancellation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deliverMessageEnvelope.ContentType);

        _logger.LogInformation("(Deliver)[{MessageId}] Send DeliverMessage to {Location}",
            deliverMessageEnvelope.Message.MessageInfo.MessageId,
            Location);

        var statusCode = await _httpClient.PostDeliverMessageEnvelopeAsync(
            Location,
            deliverMessageEnvelope,
            cancellation);

        _logger.LogDebug("POST DeliverMessage to {Location} result in: {StatusCode} {ResponseStatusCode}",
            Location,
            (int)statusCode,
            statusCode);

        return SendResultUtils.DetermineSendResultFromHttpResonse(statusCode);
    }

    /// <summary>
    /// Start sending the <see cref="NotifyMessage" />
    /// </summary>
    /// <param name="notifyMessageEnvelope"></param>
    /// <param name="cancellation"></param>
    public async Task<SendResult> SendAsync(NotifyMessageEnvelope notifyMessageEnvelope, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(notifyMessageEnvelope.ContentType))
        {
            throw new InvalidOperationException(
                $"{nameof(HttpSender)} requires a ContentType to correctly notify the message");
        }

        if (notifyMessageEnvelope.NotifyMessage == null)
        {
            throw new InvalidOperationException(
                $"{nameof(HttpSender)} requires a NotifyMessage as a series of bytes to correctly notify the message");
        }

        _logger.LogInformation("(Notify)[{MessageId}] Send Notification to {Location}",
            notifyMessageEnvelope.MessageInfo.MessageId,
            Location);

        var statusCode = await _httpClient.PostNotifyMessageEnvelopeAsync(
            Location,
            notifyMessageEnvelope,
            cancellation);

        _logger.LogDebug("POST Notification to {Location} result in: {StatusCode} {ResponseStatusCode}",
            Location,
            (int)statusCode,
            statusCode);

        return SendResultUtils.DetermineSendResultFromHttpResonse(statusCode);
    }
}
