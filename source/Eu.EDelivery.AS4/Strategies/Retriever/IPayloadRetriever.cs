
namespace Eu.EDelivery.AS4.Strategies.Retriever;

/// <summary>
/// Interface that defines how a payload must be retrieved from a certain location.
/// </summary>
public interface IPayloadRetriever
{
    /// <summary>
    /// Retrieve <see cref="Stream"/> contents from a given <paramref name="location"/>.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    Task<Stream> RetrievePayloadAsync(string location, CancellationToken cancellation);
}
