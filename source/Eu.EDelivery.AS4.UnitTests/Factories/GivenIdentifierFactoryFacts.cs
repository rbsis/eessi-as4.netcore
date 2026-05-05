using Eu.EDelivery.AS4.Factories;

namespace Eu.EDelivery.AS4.UnitTests.Factories;

/// <summary>
/// Testing <seealso cref="IdentifierFactory" />
/// </summary>
public class GivenIdentifierFactoryFacts
{
    [Fact]
    public void ThenGenerateIdGuidAndIpAddressCorrectIdGenerated()
    {
        // Act
        var id = IdentifierFactory.Create("{GUID}@{IPADDRESS}");

        // Assert
        var splittedId = id.Split('@');
        Assert.Matches(@"\w+-\w+-\w+-\w+-\w+", splittedId[0]);
        Assert.Matches(@"\d+\.\d+\.\d+\.\d+", splittedId[1]);
    }

    [Fact]
    public void ThenGenerateIdMachineNameCorrectIdGenerated()
    {
        // Act
        var id = IdentifierFactory.Create("{MACHINENAME}");

        // Assert
        Assert.NotEqual("{MACHINENAME}", id);
        Assert.Matches(@"\w+", id);
    }
}
