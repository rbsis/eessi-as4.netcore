namespace Eu.EDelivery.AS4.Strategies.Retriever;

/// <summary>
/// Interface for the Payload Provider
/// Used for mocking
/// </summary>
public interface IPayloadRetrieverProvider
{
    /// <summary>
    /// Get a specific Payload Retriever for a given Payload
    /// </summary>
    /// <param name="payload"></param>
    /// <returns></returns>
    IPayloadRetriever Get(Model.Common.Payload payload);
}
