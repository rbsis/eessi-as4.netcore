using Eu.EDelivery.AS4.Model.PMode;

namespace Eu.EDelivery.AS4.Services.DynamicDiscovery;

/// <summary>
/// Contract which is the result of a dynamic discovery operation.
/// </summary>
public class DynamicDiscoveryResult
{
    /// <summary>
    /// The complete dynamically discovered <see cref="SendingProcessingMode"/> model.
    /// </summary>
    public SendingProcessingMode CompletedSendingPMode { get; }

    /// <summary>
    /// Whether or not the ToParty should be overriden in the <see cref="Model.Submit.SubmitMessage"/>.
    /// </summary>
    public bool OverrideToParty { get; }

    private DynamicDiscoveryResult(SendingProcessingMode pmode, bool overrideToParty)
    {
        CompletedSendingPMode = pmode;
        OverrideToParty = overrideToParty;
    }

    /// <summary>
    /// Creates a <see cref="DynamicDiscoveryResult"/> based on a given <paramref name="sendingPMode"/>.
    /// </summary>
    /// <param name="sendingPMode">The pmode for which the dynamic discovery has happened.</param>
    /// <param name="overrideToParty">The value indicating whether or not the ToParty should be overriden in the <see cref="Model.Submit.SubmitMessage"/>.</param>
    public static DynamicDiscoveryResult Create(
        SendingProcessingMode sendingPMode,
        bool overrideToParty = false)
    {
        ArgumentNullException.ThrowIfNull(sendingPMode);

        return new DynamicDiscoveryResult(sendingPMode, overrideToParty);
    }
}
