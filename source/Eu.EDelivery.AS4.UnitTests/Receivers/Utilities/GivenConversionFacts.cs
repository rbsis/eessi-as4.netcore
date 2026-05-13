using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Receivers.Datastore;

namespace Eu.EDelivery.AS4.UnitTests.Receivers.Utilities;

/// <summary>
/// Testing <see cref="Conversion"/>
/// </summary>
public class GivenConversionFacts
{
    [Theory]
    [InlineData("ToBeSent", typeof(Operation))]
    [InlineData("1", typeof(int))]
    public void ConvertExpected(string value, Type expectedType)
    {
        // Act
        var actual = Conversion.Convert(expectedType, value);

        // Assert
        Assert.IsType(expectedType, actual);
    }
}
