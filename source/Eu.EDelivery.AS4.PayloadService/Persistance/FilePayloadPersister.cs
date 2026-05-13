using System.Security;
using Eu.EDelivery.AS4.PayloadService.Models;

namespace Eu.EDelivery.AS4.PayloadService.Persistance;

/// <summary>
/// <see cref="IPayloadPersister"/> implementation to persist the <see cref="Payload"/> instances on the File System.
/// </summary>
public sealed class FilePayloadPersister : IPayloadPersister
{
    private const string OriginalFileNameKey = "originalfilename:";

    private readonly string _persistenceLocation;

    private readonly ILogger<FilePayloadPersister> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilePayloadPersister"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="environment">The environment.</param>
    public FilePayloadPersister(ILogger<FilePayloadPersister> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _persistenceLocation = Path.Combine(environment.ContentRootPath, "Payloads");

        if (!Directory.Exists(_persistenceLocation))
        {
            Directory.CreateDirectory(_persistenceLocation);
        }
    }

    /// <summary>
    /// Save the given <paramref name="payload"/> with its Metadata
    /// to a specific implementation detail location.
    /// </summary>
    /// <param name="payload"></param>
    /// <returns></returns>
    public async Task<string> SavePayload(Payload payload)
    {
        var id = Guid.NewGuid();

        WritePayloadMetadata(payload.Meta, id);
        await WritePayloadContent(payload.Content, id);

        return id.ToString();
    }

    private void WritePayloadMetadata(PayloadMeta payloadMeta, Guid id)
    {
        var metaDestinationPath = GetPersistanceFileLocationOf($"{id}.meta");

        using var metaFile = File.Create(metaDestinationPath);
        using var streamWriter = new StreamWriter(metaFile);
        streamWriter.WriteLine(OriginalFileNameKey + Path.GetFileName(payloadMeta.OriginalFileName.Trim('\"')));
    }

    private async Task WritePayloadContent(Stream payload, Guid id)
    {
        using var fileStream = File.Create(GetPersistanceFileLocationOf($"{id}"));
        await payload.CopyToAsync(fileStream);
    }

    /// <summary>
    /// Load a <see cref="Payload"/> with a given <paramref name="payloadId"/> 
    /// from the specific implementation detail location.
    /// </summary>
    /// <param name="payloadId"></param>
    /// <returns></returns>
    public Task<Payload> LoadPayload(string payloadId)
    {
        var filePath = GetPersistanceFileLocationOf($"{payloadId}");

        var payload = File.Exists(filePath)
            ? new Payload(File.OpenRead(filePath), LoadPayloadMeta(payloadId))
            : Payload.Null;

        return Task.FromResult(payload);
    }

    private PayloadMeta LoadPayloadMeta(string payloadId)
    {
        var filedownloadName = $"{payloadId}.download";
        var metaFilePath = GetPersistanceFileLocationOf($"{payloadId}.meta");

        return File.Exists(metaFilePath)
            ? ParseMetadataFile(metaFilePath)
            : new PayloadMeta(filedownloadName);
    }

    private string GetPersistanceFileLocationOf(string payloadId)
    {
        return Path.Combine(_persistenceLocation, payloadId);
    }

    private static PayloadMeta ParseMetadataFile(string metaFile)
    {
        static bool LineContainsKey(string key, string line) =>
            line.IndexOf(key, StringComparison.CurrentCultureIgnoreCase) > -1;

        var originalFilename = File.ReadAllLines(metaFile)
            .Where(x => LineContainsKey(OriginalFileNameKey, x))
            .DefaultIfEmpty(string.Empty)
            .First();

        return new PayloadMeta(originalFilename);
    }

    /// <summary>
    /// Cleans up the persisted payloads older than a specified <paramref name="retentionPeriod"/>.
    /// </summary>
    /// <param name="retentionPeriod">The retention period.</param>
    public void CleanupPayloadsOlderThan(TimeSpan retentionPeriod)
    {
        var retendedFiles = Directory
            .EnumerateFiles(_persistenceLocation)
            .Select(f => new FileInfo(f))
            .Where(f => f.CreationTimeUtc > DateTimeOffset.UtcNow.Subtract(retentionPeriod));

        foreach (var f in retendedFiles)
        {
            try
            {
                f.Delete();
            }
            catch (Exception ex) when (
                ex is IOException
                || ex is SecurityException
                || ex is UnauthorizedAccessException)
            {
                _logger.LogError(ex, "CleanupPayloadsOlderThan {RetentionPeriod} failed", retentionPeriod);
            }
        }
    }
}
