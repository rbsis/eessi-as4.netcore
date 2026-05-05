using Eu.EDelivery.AS4.Strategies.Sender;

namespace Eu.EDelivery.AS4.UnitTests.Strategies.Sender;

/// <summary>
/// Testing <see cref="DeliverSenderProvider"/>
/// </summary>
public class GivenDeliverSenderProviderFacts
{
    [Theory]
    [InlineData("FILE", "c:\\temp", typeof(FileSender))]
    [InlineData("HTTP", "https://temp", typeof(HttpSender))]
    public void DeliverSenderProviderGetsSender(
        string expectedKey,
        string location,
        Type expectedSenderType)
    {
        // Arrange
        var method = new AS4.Model.PMode.Method
        {
            Type = expectedKey,
            Parameters = [new() { Name = "location", Value = location }]
        };

        // Act
        var actualSender = Default.DeliverSenderProvider.GetDeliverSender(method);

        // Assert
        Assert.IsAssignableFrom<ReliableSender>(actualSender);
        Assert.IsType(expectedSenderType, ((ReliableDeliverSender)actualSender).InnerDeliverSender);
    }

    [Fact]
    public void FailsToGetSenderIfSenderIsNotRegistered()
    {
        // Arrange
        var method = new AS4.Model.PMode.Method { Type = "not exsising key" };

        // Act / Assert
        Assert.ThrowsAny<Exception>(() => Default.DeliverSenderProvider.GetDeliverSender(method));
    }
}
