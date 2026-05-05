using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Strategies.Sender;

internal abstract class ReliableSender
{
    private readonly ILogger<ReliableSender> _logger;

    protected ReliableSender(ILogger<ReliableSender> logger)
    {
        _logger = logger;
    }

    protected async Task<SendResult> SendMessageResultAsync<T>(
        T message,
        Func<T, CancellationToken, Task<SendResult>> sending,
        string exMessage,
        CancellationToken cancellation)
    {
        try
        {
            return await sending(message, cancellation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, exMessage);
            return SendResult.FatalFail;
        }
    }
}
