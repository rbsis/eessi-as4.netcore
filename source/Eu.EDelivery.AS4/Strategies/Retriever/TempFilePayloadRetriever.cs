using Eu.EDelivery.AS4.Streaming;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Strategies.Retriever;

/// <summary>
/// Temporary <see cref="IPayloadRetriever"/> implementation that removes the file after retrieving.
/// </summary>
/// <seealso cref="IPayloadRetriever" />
public class TempFilePayloadRetriever : IPayloadRetriever
{
    public const string Key = "temp:///";

    private readonly ILogger<TempFilePayloadRetriever> _logger;

    public TempFilePayloadRetriever(ILogger<TempFilePayloadRetriever> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Retrieve <see cref="Stream"/> contents from a given <paramref name="location"/>.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<Stream> RetrievePayloadAsync(string location, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(location);

        var absolutePath = location.Replace(Key, string.Empty);

        var targetStr = await RetrieveTempFileContents(absolutePath, cancellation);
        DeleteTempFile(absolutePath);

        return targetStr;
    }

    private static async Task<Stream> RetrieveTempFileContents(string absolutePath, CancellationToken cancellation)
    {
        var virtualStr = VirtualStream.Create();

        using (var fileStr = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read))
        {
            await fileStr.CopyToAsync(virtualStr, cancellation);
        }

        virtualStr.Position = 0;
        return virtualStr;
    }

    private void DeleteTempFile(string absolutePath)
    {
        try
        {
            _logger.LogTrace("Removing temporary file at location: {AbsolutePath}", absolutePath);
            File.Delete(absolutePath);
            _logger.LogTrace("Temporary file {AbsolutePath} removed.", absolutePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete temporary file failed");
        }
    }
}
