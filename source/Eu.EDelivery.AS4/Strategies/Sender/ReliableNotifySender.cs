using Eu.EDelivery.AS4.Model.Notify;
using Eu.EDelivery.AS4.Model.PMode;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Strategies.Sender;

/// <summary>
/// Decorator to add the 'reliable' functionality of the sending functionality 
/// to both the <see cref="IDeliverSender"/> and <see cref="INotifySender"/> implementation.
/// </summary>
internal class ReliableNotifySender : ReliableSender, INotifySender
{
    internal INotifySender InnerNotifySender { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReliableNotifySender"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="notifySender"></param>
    public ReliableNotifySender(ILogger<ReliableNotifySender> logger, INotifySender notifySender) : base(logger)
    {
        InnerNotifySender = notifySender;
    }

    /// <summary>
    /// Configure the <see cref="INotifySender"/>
    /// with a given <paramref name="method"/>
    /// </summary>
    /// <param name="method"></param>
    public void Configure(Method method)
    {
        InnerNotifySender.Configure(method);
    }

    /// <summary>
    /// Start sending the <see cref="NotifyMessage"/>
    /// </summary>
    /// <param name="notifyMessage"></param>
    /// <param name="cancellation"></param>
    public async Task<SendResult> SendAsync(NotifyMessageEnvelope notifyMessage, CancellationToken cancellation) => await SendMessageResultAsync(
        message: notifyMessage,
        sending: InnerNotifySender.SendAsync,
        exMessage: $"(Notify)[{notifyMessage?.MessageInfo?.MessageId}] Unable to send NotifyMessage to the configured endpoint due to and exceptoin",
        cancellation);
}
