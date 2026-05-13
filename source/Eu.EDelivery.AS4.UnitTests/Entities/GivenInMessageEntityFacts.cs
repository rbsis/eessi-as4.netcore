using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Extensions;

namespace Eu.EDelivery.AS4.UnitTests.Entities;

public class GivenInMessageEntityFacts
{
    [Fact]
    public void InMessageHasDefaultInStatus()
    {
        Assert.Equal(default, new InMessage(Guid.NewGuid().ToString()).Status.ToEnum<InStatus>());
    }
}
