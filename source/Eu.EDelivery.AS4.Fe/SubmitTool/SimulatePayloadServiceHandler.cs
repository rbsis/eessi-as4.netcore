namespace Eu.EDelivery.AS4.Fe.SubmitTool;

/// <summary>
/// Handle dummy payloads
/// </summary>
/// <seealso cref="IPayloadHandler" />
public class SimulatePayloadServiceHandler : IPayloadHandler
{
    /// <summary>
    /// Determines whether this instance can handle the specified location.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns>
    ///   <c>true</c> if this instance can handle the specified location; otherwise, <c>false</c>.
    /// </returns>
    public bool CanHandle(string location)
    {
        return location.ToLower().StartsWith("simulate://");
    }

    /// <summary>
    /// Handles the specified location.
    /// </summary>
    /// <param name="location">The location to send to payload to.</param>
    /// <param name="fileName"></param>
    /// <param name="stream">The stream containing the payload.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    /// String containing the location of the file.
    /// </returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task<string?> HandleAsync(string location, string fileName, Stream stream, CancellationToken cancellationToken)
    {
        return Task.FromResult((string?)string.Concat(location, fileName));
    }
}
