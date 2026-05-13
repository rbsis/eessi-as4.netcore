using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Notify;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Utilities;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Strategies.Sender;

/// <summary>
/// <see cref="IDeliverSender"/>, <see cref="INotifySender"/> implementation to write contensts to the File System.
/// </summary>
[Info(Key)]
public class FileSender : IDeliverSender, INotifySender
{
    public const string Key = "FILE";

    private readonly ILogger<FileSender> _logger;

    [Info("Destination path")]
    private string? Location { get; set; }


    public FileSender(ILogger<FileSender> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Configure the <see cref="INotifySender"/>
    /// with a given <paramref name="method"/>
    /// </summary>
    /// <param name="method"></param>
    public void Configure(Method method)
    {
        var location = method["location"]?.Value;
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new InvalidOperationException(
                $"{nameof(FileSender)} requires a configured location to send the file to, please add a "
                + "<Parameter name=\"location\" value=\"your-file-path\"/> it to the applicable "
                + $"Sending or ReceivingPMode for which the {nameof(FileSender)} is configured");
        }

        Location = location;
    }

    /// <summary>
    /// Start sending the <see cref="DeliverMessage"/>
    /// </summary>
    /// <param name="deliverMessageEnvelope"></param>
    /// <param name="cancellation"></param>
    public async Task<SendResult> SendAsync(DeliverMessageEnvelope deliverMessageEnvelope, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(deliverMessageEnvelope);

        if (deliverMessageEnvelope.Message.MessageInfo.MessageId == null)
        {
            throw new InvalidOperationException(
                $"{nameof(FileSender)} requires a MessageInfo.MessageId to correctly deliver the message");
        }

        if (string.IsNullOrWhiteSpace(Location))
        {
            throw new InvalidOperationException(
                $"{nameof(FileSender)} requires a configured location to send the delivered file to, please add a "
                + "<Parameter name=\"location\" value=\"your-location\"/> it to the MessageHandling.Deliver.DeliverMethod element in the ReceivingPMode");
        }

        var directoryResult = EnsureDirectory(Location);
        if (directoryResult == SendResult.FatalFail)
        {
            return directoryResult;
        }

        var location = CombineDestinationFullName(deliverMessageEnvelope.Message.MessageInfo.MessageId, Location);
        _logger.LogTrace("Sending DeliverMessage to {Location}", location);

        var result = await TryWriteContentsToFileAsync(location, deliverMessageEnvelope.SerializeMessage(), cancellation);
        if (result == SendResult.Success)
        {
            _logger.LogInformation("(Deliver) DeliverMessage {MessageId} is successfully send to \"{Location}\"",
                deliverMessageEnvelope.Message.MessageInfo.MessageId,
                location);
        }

        return result;
    }

    /// <summary>
    /// Start sending the <see cref="NotifyMessage"/>
    /// </summary>
    /// <param name="notifyMessage"></param>
    /// <param name="cancellation"></param>
    public async Task<SendResult> SendAsync(NotifyMessageEnvelope notifyMessage, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(notifyMessage);

        if (notifyMessage.MessageInfo?.MessageId == null)
        {
            throw new InvalidOperationException(
                $"{nameof(FileSender)} requires a MessageInfo.MessageId to correctly notify the message");
        }

        if (notifyMessage.NotifyMessage == null)
        {
            throw new InvalidOperationException(
                $"{nameof(FileSender)} requires a NotifyMessage as a series of bytes to correctly notify the message");
        }


        if (string.IsNullOrWhiteSpace(Location))
        {
            throw new InvalidOperationException(
                $"{nameof(FileSender)} requires a configured location to send the notified file to, please add a "
                + "<Parameter name=\"location\" value=\"your-location\"/> it to the applicable element in the Receiving or SendingPMode");
        }

        var directoryResult = EnsureDirectory(Location);
        if (directoryResult == SendResult.FatalFail)
        {
            return directoryResult;
        }

        var location = CombineDestinationFullName(notifyMessage.MessageInfo.MessageId, Location);
        _logger.LogTrace("Sending NotifyMessage to {Location}", location);

        var result = await TryWriteContentsToFileAsync(location, notifyMessage.NotifyMessage, cancellation);
        if (result == SendResult.Success)
        {
            _logger.LogInformation(
                "(Notify) NotifyMessage {MessageId} is successfully send to \"{Location}\"",
                notifyMessage.MessageInfo.MessageId,
                location);
        }

        return result;
    }

    private static string CombineDestinationFullName(string fileName, string destinationFolder)
    {
        var filename = FilenameUtils.EnsureValidFilename(fileName) + ".xml";
        return Path.Combine(destinationFolder ?? string.Empty, filename);
    }

    private SendResult EnsureDirectory(string locationFolder)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(locationFolder) && !Directory.Exists(locationFolder))
            {
                Directory.CreateDirectory(locationFolder);
            }

            return SendResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateDirectory failed");
            return SendResult.FatalFail;
        }
    }

    private Task<SendResult> TryWriteContentsToFileAsync(string locationPath, byte[] contents, CancellationToken cancellation)
    {
        return WriteContentsToFileAsync(locationPath, contents, cancellation)
            .ContinueWith(async t =>
               {
                   if (t.IsFaulted)
                   {
                       IEnumerable<Exception>? exs = t.Exception?.Flatten().InnerExceptions;
                       if (exs == null || !exs.Any())
                       {
                           return SendResult.RetryableFail;
                       }

                       var unauthorizedEx = exs.FirstOrDefault(ex => ex is UnauthorizedAccessException);
                       if (unauthorizedEx != null)
                       {
                           _logger.LogError("A fatal error occured while uploading the file to \"{LocationPath}\": {Message}",
                               locationPath,
                               unauthorizedEx.Message);

                           return SendResult.FatalFail;
                       }

                       // Filter IOExceptions on a specific HResult.
                       // -2147024816 is the HResult if the IOException is thrown because the file already exists.
                       var fileAlreadyExsitsEx =
                           exs.FirstOrDefault(ex => ex is IOException x && x.HResult == -2147024816);
                       if (fileAlreadyExsitsEx != null)
                       {
                           _logger.LogError(fileAlreadyExsitsEx, "(Deliver) Uploading file will be retried because a file already exists with the same name");

                           // If we happen to be in a concurrent scenario where there already
                           // exists a file with the same name, try to upload the file as well.
                           // The TryUploadAttachment method will generate a new name, but it is 
                           // still possible that, under heavy load, another file has been created
                           // with the same name as the unique name that we've generated.
                           // Therefore, retry again.
                           return await TryWriteContentsToFileAsync(locationPath, contents, cancellation);
                       }

                       var message = "An error occured while uploading the file to \"{LocationPath}\": " +
                           $"{string.Join(", ", exs)}, will be retried";
                       _logger.LogError(message, locationPath);

                       return SendResult.RetryableFail;

                   }

                   if (t.IsCanceled)
                   {
                       return SendResult.RetryableFail;
                   }

                   if (t.IsCompleted)
                   {
                       return t.Result;
                   }

                   return SendResult.RetryableFail;
               }).Unwrap();
    }

    private static async Task<SendResult> WriteContentsToFileAsync(string locationPath, byte[] contents, CancellationToken cancellation)
    {
        using var fileStream = FileUtils.CreateAsync(locationPath, FileOptions.SequentialScan);
        await fileStream.WriteAsync(contents, cancellation);

        return SendResult.Success;
    }
}
