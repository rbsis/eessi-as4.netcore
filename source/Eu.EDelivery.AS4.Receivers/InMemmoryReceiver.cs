using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Receivers;

public class InMemmoryReceiver : IReceiver
{
    private Func<ReceivedMessage, CancellationToken, Task<MessagingContext>>? _messageCallback;

    public async Task<MessagingContext> AddReceivedMessageAsync(ReceivedMessage message, CancellationToken cancellation)
    {
        if (_messageCallback == null)
        {
            throw new InvalidOperationException("InMemmoryReceiver not started.");
        }

        return await _messageCallback(message, cancellation);
    }

    public void Configure(IEnumerable<Setting> settings)
    {
    }

    public void StartReceiving(Func<ReceivedMessage, CancellationToken, Task<MessagingContext>> messageCallback, CancellationToken cancellation)
    {
        _messageCallback = messageCallback;
    }

    public void StopReceiving()
    {
        _messageCallback = null;
    }
}
