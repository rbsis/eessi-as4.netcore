using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Notify;
using Eu.EDelivery.AS4.Strategies.Sender;

namespace Eu.EDelivery.AS4.UnitTests.Strategies.Sender;

/// <summary>
/// <see cref="IDeliverSender"/>, <see cref="INotifySender"/> implementation to 'Spy' on the configuration.
/// </summary>
internal class SpySender : IDeliverSender, INotifySender
{
    /// <summary>
    /// Gets a value indicating whether the <see cref="SpySender"/> is called for configuration.
    /// </summary>
    public bool IsConfigured { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this instance is delivered.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is delivered; otherwise, <c>false</c>.
    /// </value>
    public bool IsDelivered { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is notified.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is notified; otherwise, <c>false</c>.
    /// </value>
    public bool IsNotified { get; set; }

    /// <summary>
    /// Configure the <see cref="IDeliverSender"/>
    /// with a given <paramref name="method"/>
    /// </summary>
    /// <param name="method"></param>
    public void Configure(AS4.Model.PMode.Method method)
    {
        IsConfigured = true;
    }

    /// <summary>
    /// Start sending the <see cref="DeliverMessage"/>
    /// </summary>
    /// <param name="deliverMessageEnvelope"></param>
    /// <param name="cancellation"></param>
    public Task<SendResult> SendAsync(DeliverMessageEnvelope deliverMessageEnvelope, CancellationToken cancellation)
    {
        IsDelivered = true;
        return Task.FromResult(SendResult.Success);
    }

    /// <summary>
    /// Start sending the <see cref="NotifyMessage"/>
    /// </summary>
    /// <param name="notifyMessageEnvelope"></param>
    /// <param name="cancellation"></param>
    public Task<SendResult> SendAsync(NotifyMessageEnvelope notifyMessageEnvelope, CancellationToken cancellation)
    {
        IsNotified = true;
        return Task.FromResult(SendResult.Success);
    }
}

public class SpySenderFacts
{
    [Fact]
    public async Task TestIsDelivered()
    {
        // Arrange
        var sut = new SpySender();

        // Act
        await sut.SendAsync(new DeliverMessageEnvelope(new DeliverMessage(), "", []), CancellationToken.None);

        // Assert
        Assert.True(sut.IsDelivered);
    }

    [Fact]
    public async Task TestIsNotified()
    {
        // Arrange
        var sut = new SpySender();

        // Act
        await sut.SendAsync(new NotifyMessageEnvelope(new MessageInfo(), default, [], "", typeof(InMessage)), CancellationToken.None);

        // Assert
        Assert.True(sut.IsNotified);
    }

    [Fact]
    public void SpyOnConfigure()
    {
        // Arrange
        var sut = new SpySender();

        // Act
        sut.Configure(new AS4.Model.PMode.Method());

        // Assert
        Assert.True(sut.IsConfigured);
    }
}
