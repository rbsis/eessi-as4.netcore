using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.ServiceHandler;
using Eu.EDelivery.AS4.UnitTests.Agents;
using Eu.EDelivery.AS4.UnitTests.Common;
using Moq;

namespace Eu.EDelivery.AS4.UnitTests.Servicehandler;

/// <summary>
/// Testing <see cref="Kernel"/>
/// </summary>
public class GivenKernelFacts
{
    [Fact]
    public void DisposeAgentsFromKernel()
    {
        // Arrange
        var spyAgent = new SpyAgent();
        var kernel = new Kernel([spyAgent], StubConfig.Default);

        // Act
        kernel.Dispose();

        // Assert
        Assert.True(spyAgent.IsDisposed);
    }

    [Fact]
    public async Task StartAgentsFromKernel()
    {
        // Arrange
        var spyAgent = new SpyAgent();
        var kernel = new Kernel([spyAgent], StubConfig.Default);
        using var cancellationSource = new CancellationTokenSource();

        // Act
        await kernel.StartAsync(cancellationSource.Token);

        // Assert
        Assert.True(spyAgent.HasStarted);

        // TearDown
        await cancellationSource.CancelAsync();
    }

    [Fact]
    public async Task FailToStartAgentsIfConfigIsntInitializedYet()
    {
        // Arrange
        var spyAgent = new SpyAgent();
        var config = Mock.Of<IConfig>();
        Assert.False(config.IsInitialized);

        var kernel = new Kernel([spyAgent], config);

        // Act
        await kernel.StartAsync(CancellationToken.None);

        // Assert
        Assert.False(spyAgent.HasStarted);
    }
}
