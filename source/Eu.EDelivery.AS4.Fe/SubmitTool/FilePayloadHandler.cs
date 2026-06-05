namespace Eu.EDelivery.AS4.Fe.SubmitTool;

/// <summary>
/// Handle payloads by saving them to a file
/// </summary>
/// <seealso cref="IPayloadHandler" />
public class FilePayloadHandler : IPayloadHandler
{
    /// <summary>
    /// Determines whether this instance can handle the specified location.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns>
    ///   <c>true</c> if this instance can handle the specified location; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="NotImplementedException"></exception>
    public bool CanHandle(string location) => !location.Contains("http", StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    /// Handles the specified location.
    /// </summary>
    /// <param name="location">The location to send to payload to.</param>
    /// <param name="fileName"></param>
    /// <param name="stream">The stream containing the payload.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    /// String containing the location of the payload.
    /// </returns>
    public async Task<string?> HandleAsync(string location, string fileName, Stream stream, CancellationToken cancellationToken)
    {
        using var fileStream = new FileStream(Path.Combine(location, fileName), FileMode.CreateNew);
        await stream.CopyToAsync(fileStream, cancellationToken);
        return $"file:///{fileName}";
    }
}
