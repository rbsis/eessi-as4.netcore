using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Eu.EDelivery.AS4.Common;

public class TraceLoggerProvider : ILoggerProvider
{
    private bool _disposedValue;

    public ILogger CreateLogger(string categoryName) => new TraceLogger();

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
