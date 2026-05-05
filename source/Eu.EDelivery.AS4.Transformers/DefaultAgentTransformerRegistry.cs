using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Transformers;

public static class DefaultAgentTransformerRegistry
{
    private static readonly Dictionary<AgentType, TransformerConfigEntry> _registry = new()
    {
        [AgentType.Deliver] = TransformerConfigEntry<DeliverMessageTransformer>(),
        [AgentType.Submit] = TransformerConfigEntry<SubmitMessageXmlTransformer>(typeof(SubmitPayloadTransformer)),
        [AgentType.OutboundProcessing] = TransformerConfigEntry<AS4MessageTransformer>(),
        [AgentType.PushSend] = TransformerConfigEntry<OutMessageTransformer>(),
        [AgentType.PullSend] = TransformerConfigEntry<AS4MessageTransformer>(),
        [AgentType.Receive] = TransformerConfigEntry<ReceiveMessageTransformer>(),
        [AgentType.Notify] = TransformerConfigEntry<NotifyMessageTransformer>(),
        [AgentType.Forward] = TransformerConfigEntry<ForwardMessageTransformer>(),
        [AgentType.PullReceive] = TransformerConfigEntry<PModeToPullRequestTransformer>(),
    };

    private static TransformerConfigEntry TransformerConfigEntry<TDefault>(params Type[] others)
    {
        return new(TransformerConfig(typeof(TDefault)), others.Select(TransformerConfig));
    }

    private static Transformer TransformerConfig(Type t) => new() { Type = t.AssemblyQualifiedName };

    public static TransformerConfigEntry GetDefaultTransformerFor(AgentType agentType)
    {
        if (!_registry.TryGetValue(agentType, out var value))
        {
            throw new NotSupportedException($"There is no default Transformer available for agent-type {agentType}");
        }

        return value;
    }
}

/// <summary>
/// Transformer Configuration Entry to wrap the information about the different <see cref="ITransformer"/> implementations that can be used for an <see cref="AgentType"/>.
/// </summary>
public class TransformerConfigEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransformerConfigEntry" /> class.
    /// </summary>
    /// <param name="defaultTransformer">The default transformer.</param>
    /// <param name="otherTransformers">The other transformers.</param>
    public TransformerConfigEntry(Transformer defaultTransformer, IEnumerable<Transformer> otherTransformers)
    {
        DefaultTransformer = defaultTransformer;
        OtherTransformers = otherTransformers;
    }

    /// <summary>
    /// Gets the configuration to create the default <see cref="ITransformer"/> for an <see cref="AgentType"/>.
    /// </summary>
    /// <value>The default transformer.</value>
    public Transformer DefaultTransformer { get; }

    /// <summary>
    /// Gets the list of configurations to create other <see cref="ITransformer"/> implementations for an <see cref="AgentType"/>.
    /// </summary>
    /// <value>The other transformers.</value>
    public IEnumerable<Transformer> OtherTransformers { get; }
}
