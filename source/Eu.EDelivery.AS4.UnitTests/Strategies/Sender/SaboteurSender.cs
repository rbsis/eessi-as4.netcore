using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Notify;
using Eu.EDelivery.AS4.Strategies.Sender;

namespace Eu.EDelivery.AS4.UnitTests.Strategies.Sender;

/// <summary>
/// <see cref="IDeliverSender"/>, <see cref="INotifySender"/> implementation to sabotage the sending.
/// </summary>
internal class SaboteurSender : IDeliverSender, INotifySender
{
    /// <summary>
    /// Configure the <see cref="IDeliverSender" />
    /// with a given <paramref name="method" />
    /// </summary>
    /// <param name="method"></param>
    public void Configure(AS4.Model.PMode.Method method)
    {
        throw new SaboteurException("Sabotage 'Configure'");
    }

    /// <summary>
    /// Start sending the <see cref="DeliverMessage"/>
    /// </summary>
    /// <param name="deliverMessageEnvelope"></param>
    /// <param name="cancellation"></param>
    public Task<SendResult> SendAsync(DeliverMessageEnvelope? deliverMessageEnvelope, CancellationToken cancellation)
    {
        throw new SaboteurException("Sabotage 'Deliver' Send");
    }

    /// <summary>
    /// Start sending the <see cref="NotifyMessage"/>
    /// </summary>
    /// <param name="notifyMessageEnvelope"></param>
    /// <param name="cancellation"></param>
    public Task<SendResult> SendAsync(NotifyMessageEnvelope? notifyMessageEnvelope, CancellationToken cancellation)
    {
        throw new SaboteurException("Sabotage 'Notify' Send");
    }
}

public class SaboteurException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SaboteurException" /> class.
    /// </summary>
    /// <param name="message">Exception Message</param>
    public SaboteurException(string message) : base(message) { }
}

public class SaboteurSenderFacts
{
    [Fact]
    public void SabotageConfigure()
    {
        Assert.ThrowsAny<Exception>(() => new SaboteurSender().Configure(new AS4.Model.PMode.Method()));
    }

    [Fact]
    public async Task SabotageSend()
    {
        // Arrange
        var sut = new SaboteurSender();

        // Act / Assert
        await Assert.ThrowsAnyAsync<Exception>(() => sut.SendAsync(deliverMessageEnvelope: null, cancellation: CancellationToken.None));
        await Assert.ThrowsAnyAsync<Exception>(() => sut.SendAsync(notifyMessageEnvelope: null, cancellation: CancellationToken.None));
    }
}
