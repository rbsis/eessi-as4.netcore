using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Receivers;
using Eu.EDelivery.AS4.TestUtils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit.Abstractions;

namespace Eu.EDelivery.AS4.UnitTests.Receivers;

public class GivenFileReceiverFacts : IDisposable
{
    private readonly ITestOutputHelper _testLogger;
    private readonly string _watchedDirectory;
    private bool _disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="GivenFileReceiverFacts" /> class.
    /// </summary>
    /// <param name="outputHelper">The output helper.</param>
    public GivenFileReceiverFacts(ITestOutputHelper outputHelper)
    {
        _testLogger = outputHelper;
        _watchedDirectory = Path.Combine(Path.GetTempPath(), "FileReceiverTest");

        if (!Directory.Exists(_watchedDirectory))
        {
            Directory.CreateDirectory(_watchedDirectory);
        }

        FileSystemUtils.ClearDirectory(_watchedDirectory);
    }

    [Fact]
    public void BlocksFileReceiverWhenFolderIsLocked()
    {
        CreateFileInDirectory("testfile.dat", _watchedDirectory);
        CreateFileInDirectory("file.lock", _watchedDirectory);
        var receiver = CreateFileReceiver();

        Assert.Empty(StartReceiving(receiver, TimeSpan.FromSeconds(10)));
        receiver.StopReceiving();
    }

    [Fact(Skip = "Not deterministic")]
    public void DoesReceiveNonSystemFileTypes()
    {
        CreateFileInDirectory("testfile.dat", _watchedDirectory);

        var receiver = CreateFileReceiver();
        var receivedFiles = StartReceiving(receiver, TimeSpan.FromSeconds(10));
        receiver.StopReceiving();

        Assert.Single(receivedFiles);
        Assert.Equal("testfile", Path.GetFileNameWithoutExtension(receivedFiles.First()));
    }

    [Theory]
    [InlineData(".processing")]
    [InlineData(".exception")]
    [InlineData(".accepted")]
    [InlineData(".pending")]
    [InlineData(".lock")]
    public void DoesNotReceiveSystemFileTypes(string extension)
    {
        CreateFileInDirectory($"unwanted_testfile{extension}", _watchedDirectory);

        var receiver = CreateFileReceiver();
        var receivedFiles = StartReceiving(receiver, TimeSpan.FromSeconds(1));
        receiver.StopReceiving();

        Assert.Empty(receivedFiles);
    }

    private static IEnumerable<string> StartReceiving(FileReceiver receiver, TimeSpan timeout)
    {
        var signal = new ManualResetEvent(false);

        var receiveProcessor = new FileReceivedProcessor(signal);

        using var tokenSource = new CancellationTokenSource();
        tokenSource.Token.Register(receiver.StopReceiving);
        tokenSource.CancelAfter(timeout);

        Task.Factory.StartNew(() => receiver.StartReceiving((m, c) => receiveProcessor.OnFileReceived(m), tokenSource.Token), tokenSource.Token);
        signal.WaitOne(timeout);

        return receiveProcessor.ReceivedFiles.ToList();
    }

    private static void CreateFileInDirectory(string fileName, string directory)
    {
        var fullPath = Path.Combine(directory, fileName);

        File.WriteAllText(fullPath, string.Empty);
    }

    private FileReceiver CreateFileReceiver()
    {
        var settings = new FileReceiverSettings
        {
            BatchSize = 20,
            FileMask = "*.*",
            FilePath = _watchedDirectory,
            PollingInterval = TimeSpan.FromMilliseconds(100)
        };

        return new(Substitute.For<ILogger<FileReceiver>>(), Options.Create(settings));
    }

    private sealed class FileReceivedProcessor
    {
        private readonly List<string> _receivedFiles = [];
        private readonly ManualResetEvent _waitSignal;

        public IEnumerable<string> ReceivedFiles => _receivedFiles.ToArray();

        /// <summary>
        /// Initializes a new instance of the <see cref="FileReceivedProcessor"/> class.
        /// </summary>
        public FileReceivedProcessor(ManualResetEvent waitSignal)
        {
            _waitSignal = waitSignal;
        }

        public Task<MessagingContext> OnFileReceived(ReceivedMessage m)
        {
            using (var fs = (FileStream)m.UnderlyingStream)
            {
                _receivedFiles.Add(fs.Name);
            }

            _waitSignal.Set();

            return Task.FromResult(new MessagingContext(m, MessagingContextMode.Receive));
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    /// <param name="disposing"></param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                FileSystemUtils.ClearDirectory(_watchedDirectory);

                try
                {
                    Directory.Delete(_watchedDirectory, true);
                }
                catch (Exception ex)
                {
                    _testLogger.WriteLine(ex.ToString());
                }
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
