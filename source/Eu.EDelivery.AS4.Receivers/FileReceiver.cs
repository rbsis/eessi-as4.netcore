using System.ComponentModel;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Function =
    System.Func<Eu.EDelivery.AS4.Model.Internal.ReceivedMessage, System.Threading.CancellationToken,
        System.Threading.Tasks.Task<Eu.EDelivery.AS4.Model.Internal.MessagingContext>>;

namespace Eu.EDelivery.AS4.Receivers;

/// <summary>
/// <see cref="IReceiver" /> Implementation to receive Files
/// </summary>
[Info("FILE receiver")]
public class FileReceiver : PollingTemplate<FileInfo, ReceivedMessage>, IReceiver
{
    private const string FileLockName = "file.lock";

    private readonly SynchronizedCollection<(FileInfo file, string contentType)> _pendingFiles = [];

    private bool _isReceiving = false;
    private FileReceiverSettings _settings;
    /// <summary>
    /// Initializes a new instance of the <see cref="FileReceiver" /> class
    /// </summary>
    public FileReceiver(ILogger<FileReceiver> logger, IOptions<FileReceiverSettings> options) : base(logger)
    {
        _settings = options.Value;
    }

    [Info("File path", required: true)]
    [Description("Path to the folder to poll for new files")]
    private string FilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _settings.FilePath);

    [Info("File mask", required: true, defaultValue: "*.*")]
    [Description("Mask used to match files.")]
    private string FileMask => _settings.FileMask;

    [Info("Batch size", required: true, defaultValue: SettingKeys.BatchSizeDefault)]
    [Description("Indicates how many files should be processed per batch.")]
    private int BatchSize => _settings.BatchSize;

    [Info("Polling interval", defaultValue: SettingKeys.PollingIntervalDefault)]
    protected override TimeSpan PollingInterval => _settings.PollingInterval;

    private static readonly string[] _excludedExtensions = [".pending", ".processing", ".accepted", ".exception", ".details", ".lock"];

    #region Configuration

    private static class SettingKeys
    {
        public const string FilePath = "FilePath";
        public const string FileMask = "FileMask";
        public const string BatchSize = "BatchSize";
        public const string BatchSizeDefault = "20";
        public const string PollingInterval = "PollingInterval";
        public const string PollingIntervalDefault = "00:00:03";
    }

    /// <summary>
    /// Configure the receiver with a given settings dictionary.
    /// </summary>
    /// <param name="settings"></param>
    void IReceiver.Configure(IEnumerable<Setting> settings)
    {
        var properties = settings.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        var configuredBatchSize = properties.ReadOptionalProperty(SettingKeys.BatchSize, SettingKeys.BatchSizeDefault);

        if (!int.TryParse(configuredBatchSize, out var batchSize))
        {
            batchSize = 20;
        }

        _settings = new FileReceiverSettings
        {
            FilePath = properties.ReadMandatoryProperty(SettingKeys.FilePath),
            FileMask = properties.ReadOptionalProperty(SettingKeys.FileMask, "*.*"),
            BatchSize = batchSize,
            PollingInterval = ReadPollingIntervalFromProperties(properties)
        };

        if (!Directory.Exists(FilePath))
        {
            _logger.LogWarning("Directory: '{FilePath}' does not exists", FilePath);
        }
    }

    private static TimeSpan ReadPollingIntervalFromProperties(Dictionary<string, string> properties)
    {
        if (!properties.TryGetValue(SettingKeys.PollingInterval, out var pollingInterval))
        {
            return TimeSpan.Parse(SettingKeys.PollingIntervalDefault);
        }

        return pollingInterval.AsTimeSpan(TimeSpan.Parse(SettingKeys.PollingIntervalDefault));
    }

    #endregion

    /// <summary>
    /// Start Receiving on the given File LocationParameter
    /// </summary>
    /// <param name="messageCallback"></param>
    /// <param name="cancellation"></param>
    public void StartReceiving(Function messageCallback, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(messageCallback);

        _isReceiving = true;
        _logger.LogDebug("Start receiving on \"{Path}\" ...", Path.GetFullPath(FilePath));
        StartPolling(messageCallback, cancellation);
    }

    /// <summary>
    /// Stop the <see cref="IReceiver"/> instance from receiving.
    /// </summary>
    public void StopReceiving()
    {
        _isReceiving = false;
        _logger.LogDebug("Stop receiving on \"{Path}\"", Path.GetFullPath(FilePath));
    }

    /// <summary>
    /// Declaration to where the Message are and can be polled
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override IEnumerable<FileInfo> GetMessagesToPoll(CancellationToken cancellationToken)
    {
        if (AddLockFile() == FileLock.Failure)
        {
            return [];
        }

        var directoryInfo = new DirectoryInfo(FilePath);
        var resultedFiles = new List<FileInfo>();

        if (cancellationToken.IsCancellationRequested || !_isReceiving)
        {
            return [];
        }

        var directoryFiles =
            directoryInfo.GetFiles(FileMask)
                         .Where(fi => !_excludedExtensions.Contains(fi.Extension))
                         .Take(BatchSize).ToArray();

        try
        {
            foreach (var file in directoryFiles)
            {
                try
                {
                    var contentType = MimeTypes.GetMimeType(file.Name);
                    var (success, filename) = MoveFile(file, "pending");

                    if (success)
                    {
                        var pendingFile = new FileInfo(filename);

                        _logger.LogTrace("Locked file {File} to be processed and renamed it to {PendingFile}",
                            file.Name,
                            pendingFile.Name);

                        _pendingFiles.Add((pendingFile, contentType));

                        resultedFiles.Add(pendingFile);
                    }
                }
                catch (IOException ex)
                {
                    _logger.LogInformation(ex, "FileReceiver on \"{File}\" skipped since it is in use.", file.FullName);
                }
            }
        }
        finally
        {
            RemoveFileLock();
        }

        return resultedFiles;
    }

    private enum FileLock { Created, Failure }

    private FileLock AddLockFile()
    {
        try
        {
            using var fs = new FileStream(
                Path.Combine(FilePath, FileLockName),
                FileMode.CreateNew,
                FileAccess.Write);
            fs.Close();

            return FileLock.Created;
        }
        catch (IOException ex)
        {
            _logger.LogTrace(ex, "The lock file cannot be added");
            return FileLock.Failure;
        }
    }

    private void RemoveFileLock()
    {
        try
        {
            File.Delete(Path.Combine(FilePath, FileLockName));
        }
        catch (IOException ex)
        {
            _logger.LogTrace(ex, "The lock file cannot be removed");
        }
    }

    /// <summary>
    /// Declaration to the action that has to executed when a Message is received
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="messageCallback">Message Callback after the Message is received</param>
    /// <param name="token"></param>
    protected override async Task MessageReceivedAsync(FileInfo entity, Function messageCallback, CancellationToken token)
    {
        _logger.LogInformation("Received message from Filesystem: \"{Entity}\"", entity.Name);
        if (!entity.Exists)
        {
            return;
        }


        var item = _pendingFiles.FirstOrDefault(f => f.file == entity);
        await OpenStreamFromMessage(item, messageCallback, token);
        _pendingFiles.Remove(item);
    }

    private async Task OpenStreamFromMessage(
        (FileInfo fileInfo, string contentType) _,
        Function messageCallback,
        CancellationToken token)
    {
        try
        {
            var (success, filename) = MoveFile(_.fileInfo, "processing");
            if (success)
            {
                MessagingContext? messagingContext = null;

                try
                {
                    using (var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                    {
                        fileStream.Seek(0, SeekOrigin.Begin);

                        var receivedMessage = new ReceivedMessage(
                            underlyingStream: fileStream,
                            contentType: _.contentType,
                            origin: filename,
                            length: _.fileInfo.Length);
                        messagingContext = await messageCallback(receivedMessage, token);
                    }

                    await NotifyReceivedFile(_.fileInfo, messagingContext);
                }
                finally
                {
                    messagingContext?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while processing \"{Name}\"", _.fileInfo.Name);
        }
    }

    private async Task NotifyReceivedFile(FileInfo fileInfo, MessagingContext messagingContext)
    {
        if (messagingContext.Exception != null)
        {
            await HandleException(fileInfo, messagingContext.Exception);
        }
        else
        {
            MoveFile(fileInfo, "accepted");
        }
    }

    private async Task HandleException(FileInfo fileInfo, Exception exception)
    {
        MoveFile(fileInfo, "exception");
        await CreateExceptionFile(fileInfo, exception);
    }

    private async Task CreateExceptionFile(FileSystemInfo fileInfo, Exception exception)
    {
        var fileName = fileInfo.FullName + ".details";
        _logger.LogInformation("Exception Details are stored at: \"{FileName}\"", fileName);

        using var streamWriter = new StreamWriter(fileName);
        await streamWriter.WriteLineAsync(exception.ToString());
    }

    protected override void ReleasePendingItems()
    {
        // Rename the 'pending' files to their original filename.
        var extension = Path.GetExtension(FileMask);

        lock (_pendingFiles.SyncRoot)
        {
            for (var i = _pendingFiles.Count - 1; i >= 0; i--)
            {
                var item = _pendingFiles[i];

                if (File.Exists(item.file.FullName))
                {
                    MoveFile(item.file, extension);
                }

                _pendingFiles.Remove(item);
            }
        }
    }

    /// <summary>
    /// Describe what to do in case of an Exception
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="exception"></param>
    protected override void HandleMessageException(FileInfo fileInfo, Exception exception)
    {
        _logger.LogError(exception, "HandleMessage failed");
        MoveFile(fileInfo, "exception");
    }

    /// <summary>
    /// Move file to another place on the File System
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="extension"></param>
    private (bool success, string filename) MoveFile(FileInfo fileInfo, string extension)
    {
        extension = extension.TrimStart('.');

        _logger.LogTrace("Renaming file '{Name}'...", fileInfo.Name);
        var destFileName =
            $"{fileInfo.Directory?.FullName}\\{Path.GetFileNameWithoutExtension(fileInfo.FullName)}.{extension}";

        try
        {
            destFileName = FilenameUtils.EnsureFilenameIsUnique(destFileName);

            var attempts = 0;

            do
            {
                try
                {
                    fileInfo.MoveTo(destFileName);
                    attempts = 5;
                }
                catch (IOException)
                {
                    // When the file is in use, an IO exception will be thrown.
                    // If that is the case, wait a little and retry.                       
                    if (attempts == 5)
                    {
                        throw;
                    }
                    attempts++;
                    Thread.Sleep(500);
                }
            } while (attempts < 5);

            _logger.LogTrace("File renamed to: '{Name}'", fileInfo.Name);

            return (success: true, filename: destFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to MoveFile \"{FullName}\" to \"{DestFileName}\"",
                fileInfo.FullName,
                destFileName);
            return (success: false, filename: string.Empty);
        }
    }

}
