namespace Eu.EDelivery.AS4.Fe.SubmitTool;

/// <summary>
/// Payload handler interface
/// </summary>
/// <seealso cref="IHandler" />
public interface IPayloadHandler : IHandler
{
    /// <summary>
    /// Handles the specified location.
    /// </summary>
    /// <param name="location">The location to send to payload to.</param>
    /// <param name="fileName"></param>
    /// <param name="stream">The stream containing the payload.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>String containing the location of the file.</returns>
    Task<string?> HandleAsync(string location, string fileName, Stream stream, CancellationToken cancellationToken);
}
