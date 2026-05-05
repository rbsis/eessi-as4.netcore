using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Transformers;

namespace Eu.EDelivery.AS4.UnitTests.Transformers;

internal class SaboteurTransformer : ITransformer
{
    /// <summary>
    /// Configures the <see cref="ITransformer"/> implementation with specific user-defined properties.
    /// </summary>
    /// <param name="properties">The properties.</param>
    public void Configure(IDictionary<string, string> properties) { }

    /// <summary>
    /// Transform a given <see cref="ReceivedMessage"/> to a Canonical <see cref="MessagingContext"/> instance.
    /// </summary>
    /// <param name="message">Given message to transform.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public Task<MessagingContext> TransformAsync(ReceivedMessage message, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }
}
