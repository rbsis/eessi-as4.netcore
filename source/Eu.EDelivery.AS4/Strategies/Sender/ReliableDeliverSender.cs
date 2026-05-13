using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.PMode;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Strategies.Sender;

/// <summary>
/// Decorator to add the 'reliable' functionality of the sending functionality 
/// to both the <see cref="IDeliverSender"/> and <see cref="INotifySender"/> implementation.
/// </summary>
internal class ReliableDeliverSender : ReliableSender, IDeliverSender
{
    internal IDeliverSender InnerDeliverSender { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReliableDeliverSender"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="deliverSender"></param>
    public ReliableDeliverSender(ILogger<ReliableDeliverSender> logger, IDeliverSender deliverSender) : base(logger)
    {
        InnerDeliverSender = deliverSender;
    }

    /// <summary>
    /// Configure the <see cref="INotifySender"/>
    /// with a given <paramref name="method"/>
    /// </summary>
    /// <param name="method"></param>
    public void Configure(Method method)
    {
        InnerDeliverSender.Configure(method);
    }

    /// <summary>
    /// Start sending the <see cref="DeliverMessage"/>
    /// </summary>
    /// <param name="envelope"></param>
    /// <param name="cancellation"></param>
    public async Task<SendResult> SendAsync(DeliverMessageEnvelope envelope, CancellationToken cancellation) => await SendMessageResultAsync(
        message: envelope,
        sending: InnerDeliverSender.SendAsync,
        exMessage: $"(Deliver)[{envelope.Message.MessageInfo.MessageId}] Unable to send DeliverMessage to the configured endpoint due to an exception",
        cancellation);
}
