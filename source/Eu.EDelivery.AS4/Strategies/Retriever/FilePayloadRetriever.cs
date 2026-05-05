using Eu.EDelivery.AS4.Common;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Strategies.Retriever;

/// <summary>
/// File Retriever Implementation to retrieve the FileStream of a local file
/// </summary>
public class FilePayloadRetriever : IPayloadRetriever
{
    public const string Key = "file:///";

    private readonly ILogger<FilePayloadRetriever> _logger;

    private readonly IConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilePayloadRetriever"/> class.
    /// </summary>
    public FilePayloadRetriever(ILogger<FilePayloadRetriever> logger, IConfig configuration)
    {
        _logger = logger;
        _config = configuration;
    }

    /// <summary>
    /// Retrieve <see cref="Stream"/> contents from a given <paramref name="location"/>.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public Task<Stream> RetrievePayloadAsync(string location, CancellationToken cancellation)
    {
        var relativePayloadPath = location.Replace(Key, string.Empty);
        var absolutePayloadPath = Path.GetFullPath(Path.Combine(Config.ApplicationPath, relativePayloadPath));

        var payload = new FileInfo(absolutePayloadPath);

        var relativeRetrievalPath = _config.PayloadRetrievalLocation.Replace(Key, string.Empty);
        var absoluteRetrievalPath = Path.GetFullPath(relativeRetrievalPath);

        if (!StringComparer.OrdinalIgnoreCase.Equals(payload.Directory?.FullName, absoluteRetrievalPath))
        {
            throw new NotSupportedException(
                $"Only files from the '{_config.PayloadRetrievalLocation}' folder are allowed to be retrieved: {payload.Directory?.FullName} <> {absoluteRetrievalPath}");
        }

        var uri = new Uri(absolutePayloadPath);
        Stream payloadStream = new FileStream(uri.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        _logger.LogDebug("Payload is successfully retrieved at location \"{Location}\"", location);
        return Task.FromResult(payloadStream);
    }
}
