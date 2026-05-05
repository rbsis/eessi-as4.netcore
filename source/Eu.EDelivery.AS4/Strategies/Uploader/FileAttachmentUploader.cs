using System.ComponentModel;
using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Utilities;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Eu.EDelivery.AS4.Strategies.Uploader;

/// <summary>
/// <see cref="IAttachmentUploader" /> implementation to upload attachments to the file system
/// </summary>
[Info(FileAttachmentUploader.Key)]
public class FileAttachmentUploader : IAttachmentUploader
{
    public const string Key = "FILE";

    private readonly ILogger<FileAttachmentUploader> _logger;

    private Method? _method;

    [Info("Location")]
    [Description("Folder where the payloads must be delivered")]
    private string? Location => _method?["location"]?.Value;

    [Info("Payload Naming Pattern")]
    [Description(PayloadFileNameFactory.PatternDocumentation)]
    private string? NamePattern => _method?["filenameformat"]?.Value;

    [Info("Allow overwrite")]
    [Description(
        "When Allow overwrite is set to true, the file will be overwritten if it already exists.\n\r" +
        "When set to false, an attempt will be made to create a new unique filename. The default is false.")]
    private string? AllowOverwrite => _method?["allowoverwrite"]?.Value;

    public FileAttachmentUploader(ILogger<FileAttachmentUploader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Configure the <see cref="IAttachmentUploader" />
    /// with a given <paramref name="payloadReferenceMethod" />
    /// </summary>
    /// <param name="payloadReferenceMethod"></param>
    public void Configure(Method payloadReferenceMethod)
    {
        ArgumentNullException.ThrowIfNull(payloadReferenceMethod);

        _method = payloadReferenceMethod;
    }

    /// <inheritdoc/>
    public Task<UploadResult?> UploadAsync(Attachment attachment, MessageInfo referringUserMessage, CancellationToken cancellation)
    {
        var downloadUrl = AssembleFileDownloadUrlFor(attachment, referringUserMessage);
        if (downloadUrl == null)
        {
            _logger.LogDebug("Upload failed with fatal fail: No download URL could be assembled to download the attachment from");
            return Task.FromResult<UploadResult?>(UploadResult.FatalFail);
        }

        var attachmentFilePath = Path.GetFullPath(downloadUrl);

        var allowOverwrite = DetermineAllowOverwrite();
        return TryUploadAttachmentAsync(attachment, attachmentFilePath, allowOverwrite);
    }

    private string? AssembleFileDownloadUrlFor(Attachment attachment, MessageInfo referringUserMessage)
    {
        try
        {
            _ = MimeTypes.TryGetExtension(attachment.ContentType, out var extension);
            var fileName = PayloadFileNameFactory.CreateFileName(NamePattern, attachment, referringUserMessage);
            var validFileName = FilenameUtils.EnsureValidFilename($"{fileName}{extension}");

            return Location != null ? Path.Combine(Location, validFileName) : validFileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An fatal error occured while determining the file path");
            return null;
        }
    }

    private bool DetermineAllowOverwrite()
    {
        if (string.IsNullOrEmpty(AllowOverwrite))
        {
            return false;
        }

        if (bool.TryParse(AllowOverwrite, out var allowOverwrite))
        {
            return allowOverwrite;
        }

        return false;
    }

    private async Task<UploadResult?> TryUploadAttachmentAsync(Attachment attachment, string attachmentFilePath, bool allowOverwrite)
    {
        return await UploadAttachmentAsync(attachment, attachmentFilePath, allowOverwrite)
            .ContinueWith(async t =>
            {
                if (t.IsFaulted)
                {
                    IEnumerable<Exception>? exs = t.Exception?.Flatten().InnerExceptions;
                    if (exs == null || !exs.Any())
                    {
                        return UploadResult.RetryableFail;
                    }

                    var unauthorizedEx = exs.FirstOrDefault(ex => ex is UnauthorizedAccessException);
                    if (unauthorizedEx != null)
                    {
                        _logger.LogError(unauthorizedEx, "A fatal error occured while uploading the attachment {AttachmentId}", attachment.Id);

                        return UploadResult.FatalFail;
                    }

                    // Filter IOExceptions on a specific HResult.
                    // -2147024816 is the HResult if the IOException is thrown because the file already exists.
                    var fileAlreadyExsitsEx =
                        exs.FirstOrDefault(ex => ex is IOException x && x.HResult == -2147024816);
                    if (fileAlreadyExsitsEx != null)
                    {
                        _logger.LogError(fileAlreadyExsitsEx, "Uploading file will be retried because a file already exists with the same name.");

                        // If we happen to be in a concurrent scenario where there already
                        // exists a file with the same name, try to upload the file as well.
                        // The TryUploadAttachment method will generate a new name, but it is 
                        // still possible that, under heavy load, another file has been created
                        // with the same name as the unique name that we've generated.
                        // Therefore, retry again.
                        return await TryUploadAttachmentAsync(attachment, attachmentFilePath, allowOverwrite);
                    }

                    var desc = string.Join(", ", exs);
                    _logger.LogError("An error occured while uploading the attachment {AttachmentId}: {Desc}, will be retried", attachment.Id, desc);

                    return UploadResult.RetryableFail;
                }

                if (t.IsCanceled)
                {
                    return UploadResult.RetryableFail;
                }

                if (t.IsCompleted)
                {
                    return t.Result;
                }

                return UploadResult.RetryableFail;
            }).Unwrap();
    }

    private async Task<UploadResult> UploadAttachmentAsync(Attachment attachment, string attachmentFilePath, bool overwriteExisting)
    {
        // Create the directory, if it does not exist.
        var directoryName = Path.GetDirectoryName(attachmentFilePath)
            ?? throw new InvalidOperationException("GetDirectoryName failed.");

        Directory.CreateDirectory(directoryName);

        (var fileMode, var filePath) = overwriteExisting
            ? (FileMode.Create, attachmentFilePath)
            : (FileMode.CreateNew, FilenameUtils.EnsureFilenameIsUnique(attachmentFilePath));

        _logger.LogTrace("Trying to upload attachment {AttachmentId} to {AttachmentFilePath}", attachment.Id, attachmentFilePath);

        using (var fileStream = new FileStream(
            filePath,
            fileMode,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await attachment.Content.CopyToAsync(fileStream);
        }

        _logger.LogInformation("(Deliver) Attachment {AttachmentId} is uploaded successfully to \"{AttachmentFilePath}\"", attachment.Id, attachmentFilePath);
        return UploadResult.SuccessWithUrl(attachmentFilePath);
    }
}
