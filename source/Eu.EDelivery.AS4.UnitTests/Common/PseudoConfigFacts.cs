using Eu.EDelivery.AS4.TestUtils.Stubs;

namespace Eu.EDelivery.AS4.UnitTests.Common;

public class PseudoConfigFacts : PseudoConfig
{
    [Fact]
    public void FailsToInitialize()
    {
        Assert.False(new PseudoConfig().IsInitialized);
        Assert.ThrowsAny<Exception>(() =>
        {
            Initialize("settings.xml");
        });
    }

    [Fact]
    public void FailsToGetAgents()
    {
        Assert.ThrowsAny<Exception>(GetEnabledMinderTestAgents);
        Assert.ThrowsAny<Exception>(GetAgentsConfiguration);
    }

    [Fact]
    public void FailsToGetPModes()
    {
        Assert.ThrowsAny<Exception>(GetReceivingPModes);
        Assert.ThrowsAny<Exception>(() => GetSendingPMode("ignored string"));
        Assert.ThrowsAny<Exception>(() => ContainsSendingPMode("ignored string"));
    }

    [Fact]
    public void FailsToGetSetting()
    {
        Assert.ThrowsAny<Exception>(() => GetSetting("ignored string"));
    }
}
