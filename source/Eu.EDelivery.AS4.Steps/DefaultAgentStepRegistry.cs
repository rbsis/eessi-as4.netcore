using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Steps.Deliver;
using Eu.EDelivery.AS4.Steps.Forward;
using Eu.EDelivery.AS4.Steps.Notify;
using Eu.EDelivery.AS4.Steps.Receive;
using Eu.EDelivery.AS4.Steps.Send;
using Eu.EDelivery.AS4.Steps.Submit;

namespace Eu.EDelivery.AS4.Steps;

internal class DefaultAgentStepRegistry : IDefaultAgentStepRegistry
{
    private static readonly Dictionary<AgentType, StepConfiguration> _stepConfiguration = new()
    {
        [AgentType.Submit] = new()
        {
            NormalPipeline =
            [
                new() { Type = typeof(RetrieveSendingPModeStep).AssemblyQualifiedName! },
                new() { Type = typeof(DynamicDiscoveryStep).AssemblyQualifiedName! },
                new() { Type = typeof(CreateAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(StoreAS4MessageStep).AssemblyQualifiedName! }
            ]
        },
        [AgentType.OutboundProcessing] = new()
        {
            NormalPipeline =
            [
                new() { Type = typeof(CompressAttachmentsStep).AssemblyQualifiedName! },
                new() { Type = typeof(SignAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(EncryptAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(SetMessageToBeSentStep).AssemblyQualifiedName! }
            ]
        },
        [AgentType.PullSend] = new()
        {
            NormalPipeline =
            [
                new() { Type = typeof(VerifySignatureAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(VerifyPullRequestAuthorizationStep).AssemblyQualifiedName! },
                new() { Type = typeof(SaveReceivedMessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(DeterminePModesStep).AssemblyQualifiedName! },
                new() { Type = typeof(UpdateReceivedAS4MessageBodyStep).AssemblyQualifiedName! },
                new() { Type = typeof(SelectUserMessageToSendStep).AssemblyQualifiedName! }
            ]
        },
        [AgentType.PushSend] = new()
        {
            NormalPipeline =
            [
                new() { Type = typeof(SendAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(SaveReceivedMessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(DeterminePModesStep).AssemblyQualifiedName! },
                new() { Type = typeof(VerifySignatureAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(UpdateReceivedAS4MessageBodyStep).AssemblyQualifiedName! }
            ],
            ErrorPipeline =
            [
                new() { Type = typeof(LogReceivedProcessingErrorStep).AssemblyQualifiedName! }
            ]
        },
        [AgentType.Receive] = new()
        {
            NormalPipeline =
            [
                new() { Type = typeof(SaveReceivedMessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(DeterminePModesStep).AssemblyQualifiedName! },
                new() { Type = typeof(ValidateAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(DecryptAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(VerifySignatureAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(DecompressAttachmentsStep).AssemblyQualifiedName! },
                new() { Type = typeof(UpdateReceivedAS4MessageBodyStep).AssemblyQualifiedName! },
                new() { Type = typeof(CreateAS4ReceiptStep).AssemblyQualifiedName! },
                new() { Type = typeof(SignAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(CreateAS4SignalMessageStep).AssemblyQualifiedName! }
            ],
            ErrorPipeline =
            [
                new() { Type = typeof(CreateAS4ErrorStep).AssemblyQualifiedName! },
                new() { Type = typeof(SignAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(CreateAS4SignalMessageStep).AssemblyQualifiedName! }
            ]
        },
        [AgentType.PullReceive] = new()
        {
            NormalPipeline =
            [
                new() { Type = typeof(BundleSignalMessageToPullRequestStep).AssemblyQualifiedName! },
                new() { Type = typeof(SignAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(SendAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(SaveReceivedMessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(DeterminePModesStep).AssemblyQualifiedName! },
                new() { Type = typeof(ValidateAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(DecryptAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(VerifySignatureAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(DecompressAttachmentsStep).AssemblyQualifiedName! },
                new() { Type = typeof(UpdateReceivedAS4MessageBodyStep).AssemblyQualifiedName! },
                new() { Type = typeof(CreateAS4ReceiptStep).AssemblyQualifiedName! },
                new() { Type = typeof(SignAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(CreateAS4SignalMessageStep).AssemblyQualifiedName! }
            ],
            ErrorPipeline =
            [
                new() { Type = typeof(CreateAS4ErrorStep).AssemblyQualifiedName! },
                new() { Type = typeof(SignAS4MessageStep).AssemblyQualifiedName! },
                new() { Type = typeof(CreateAS4SignalMessageStep).AssemblyQualifiedName! }
            ]
        },
        [AgentType.Forward] = new()
        {
            NormalPipeline =
            [
                new() { Type = typeof(DetermineRoutingStep).AssemblyQualifiedName! },
                new() { Type = typeof(DynamicDiscoveryStep).AssemblyQualifiedName! },
                new() { Type = typeof(CreateForwardMessageStep).AssemblyQualifiedName! }
            ]
        },
        [AgentType.Deliver] = new()
        {
            NormalPipeline =
            [
                new() { Type = typeof(UploadAttachmentsStep).AssemblyQualifiedName! },
                new() { Type = typeof(SendDeliverMessageStep).AssemblyQualifiedName! }
            ]
        },
        [AgentType.Notify] = new()
        {
            NormalPipeline =
            [
                new() { Type = typeof(SendNotifyMessageStep).AssemblyQualifiedName! },
            ]
        },
    };

    /// <summary>
    /// Gets the default implementation of the <see cref="StepConfiguration"/> for the given <paramref name="agentType"/>.
    /// </summary>
    /// <param name="agentType">Type of the agent.</param>
    /// <returns></returns>
    public StepConfiguration GetDefaultStepConfiguration(AgentType agentType)
    {
        if (!_stepConfiguration.TryGetValue(agentType, out var value))
        {
            throw new NotSupportedException($"There is no default StepConfiguration available for agent-type {agentType}");
        }

        return value;
    }
}
