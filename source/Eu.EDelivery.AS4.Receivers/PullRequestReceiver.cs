using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Receivers.PullRequest;
using Eu.EDelivery.AS4.Serialization;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Receivers;

/// <summary>
/// <see cref="IReceiver" /> implementation to pull exponentially for Pull Requests.
/// </summary>
public class PullRequestReceiver : ExponentialIntervalReceiver<PModePullRequest>
{
    private readonly IConfig _configuration;

    private Func<PModePullRequest, Task<MessagingContext>>? _messageCallback;

    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestReceiver" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="configuration"><see cref="IConfig" /> implementation to collection PModes.</param>
    public PullRequestReceiver(ILogger<PullRequestReceiver> logger, IConfig configuration) : base(logger)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Configure the receiver with a given settings dictionary.
    /// </summary>
    /// <param name="settings"></param>
    public override void Configure(IEnumerable<Setting> settings)
    {
        foreach (var setting in settings)
        {
            if (!_configuration.ContainsSendingPMode(setting.Key))
            {
                _logger.LogWarning("Configured SendingPMode {Key} could not be found", setting.Key);
                continue;
            }

            var pmode = _configuration.GetSendingPMode(setting.Key);
            var minTimeSpan = setting["tmin"].AsTimeSpan();
            var maxTimeSpan = setting["tmax"].AsTimeSpan();

            if (minTimeSpan != default && maxTimeSpan != default)
            {
                var pullRequest = new PModePullRequest(pmode!, minTimeSpan, maxTimeSpan);
                AddIntervalRequest(pullRequest);
            }
        }
    }

    /// <summary>
    /// Start receiving on a configured Target
    /// Received messages will be send to the given Callback
    /// </summary>
    /// <param name="messageCallback"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="Exception">A delegate callback throws an exception.</exception>
    public override void StartReceiving(
        Func<ReceivedMessage, CancellationToken, Task<MessagingContext>> messageCallback,
        CancellationToken cancellationToken)
    {
        _messageCallback = async message =>
        {
            var receivedMessage = new ReceivedMessage(
                underlyingStream: await AS4XmlSerializer.ToStreamAsync(message.PMode, cancellationToken),
                contentType: Constants.ContentTypes.Soap,
                origin: message.PMode?.PushConfiguration?.Protocol?.Url ?? "unknown");

            return await messageCallback(receivedMessage, cancellationToken);
        };

        // Wait some time till the Kernel is fully started
        Thread.Sleep(TimeSpan.FromSeconds(5));

        StartInterval();
    }

    /// <summary>
    /// <paramref name="intervalPullRequest" /> is received.
    /// </summary>
    /// <param name="intervalPullRequest"></param>
    /// <returns></returns>
    /// <exception cref="Exception">A delegate callback throws an exception.</exception>
    protected override async Task<Interval> OnRequestReceived(PModePullRequest intervalPullRequest)
    {
        var resultedMessage = await _messageCallback!(intervalPullRequest);

        try
        {
            var isUserMessage = resultedMessage.AS4Message?.IsUserMessage == true;
            var intervalResult = isUserMessage ? Interval.Reset : Interval.Increase;
            var message = $"PullRequest result in {(isUserMessage ? "UserMessage" : "Error")} next interval will be " + "\"{intervalResult}\"";
            _logger.LogInformation(message, isUserMessage, intervalResult);

            return intervalResult;
        }
        finally
        {
            resultedMessage?.Dispose();
        }
    }
}
