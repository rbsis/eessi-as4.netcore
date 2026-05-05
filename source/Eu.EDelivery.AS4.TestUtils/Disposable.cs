namespace Eu.EDelivery.AS4.TestUtils;

public class Disposable : IDisposable
{
    private bool _disposedValue;

    private readonly Action _onDispose;

    private Disposable(Action onDispose)
    {
        _onDispose = onDispose;
    }

    /// <summary>
    /// Create an <see cref="IDisposable"/> implementation which runs a given <see cref="Action"/> function when disposing the instance.
    /// </summary>
    /// <param name="onDispose">Function to run on disposal of the instance.</param>
    /// <returns></returns>
    public static IDisposable Create(Action onDispose)
    {
        return new Disposable(onDispose);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _onDispose();
            }

            _disposedValue = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
