using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Extensions;

namespace Eu.EDelivery.AS4.UnitTests.Entities;

public class GivenOutMessageEntityFacts
{
    [Fact]
    public void OutMessageHasDefaultInStatus()
    {
        Assert.Equal(default, new OutMessage(Guid.NewGuid().ToString()).Status.ToEnum<OutStatus>());
    }
}
