using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Transformers;

public class OutMessageTransformer : ITransformer
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
    public async Task<MessagingContext> TransformAsync(ReceivedMessage message, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(message);

        var context = new MessagingContext(message, MessagingContextMode.Send);
        return await Task.FromResult(context);
    }
}