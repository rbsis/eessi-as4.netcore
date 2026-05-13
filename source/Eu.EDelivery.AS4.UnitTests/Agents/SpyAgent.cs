using Eu.EDelivery.AS4.Agents;

namespace Eu.EDelivery.AS4.UnitTests.Agents;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3881:\"IDisposable\" should be implemented correctly", Justification = "<Pending>")]
internal class SpyAgent : IAgent, IDisposable
{
    private readonly EventWaitHandle _waitHandle = new ManualResetEvent(initialState: false);

    /// <summary>
    /// Gets the agent configuration.
    /// </summary>
    /// <value>
    /// The agent configuration.
    /// </value>
    public AgentConfig AgentConfig { get; } = new("Spy");

    /// <summary>
    /// Gets a value indicating whether this instance is stopped.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is stopped; otherwise, <c>false</c>.
    /// </value>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this instance is stopped.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is stopped; otherwise, <c>false</c>.
    /// </value>
    public bool IsStopped { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance has started.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has started; otherwise, <c>false</c>.
    /// </value>
    public bool HasStarted => _waitHandle.WaitOne(TimeSpan.FromSeconds(1));

    /// <summary>
    /// Starts the specified cancellation token.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _waitHandle.Set();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops this instance.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        IsStopped = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        IsDisposed = true;
    }
}

public class SpyAgentFacts
{
    [Fact]
    public void SpyOnDisposing()
    {
        // Arrange
        var sut = new SpyAgent();

        // Act
        sut.Dispose();

        // Assert
        Assert.True(sut.IsDisposed);
    }

    [Fact]
    public async Task SpyOnStarting()
    {
        // Arrange
        var sut = new SpyAgent();

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        Assert.True(sut.HasStarted);
    }

    [Fact]
    public async Task SpyOnStopping()
    {
        // Arrange
        var sut = new SpyAgent();

        // Act
        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(sut.IsStopped);
    }
}
