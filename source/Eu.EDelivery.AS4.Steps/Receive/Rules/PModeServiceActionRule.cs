using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;

namespace Eu.EDelivery.AS4.Steps.Receive.Rules;

/// <summary>
/// PMode Rule to check if the PMode Service/Action is equal to the UserMessage Service/Action
/// </summary>
internal class PModeServiceActionRule : IPModeRule
{
    private const int Points = 3;
    private const int NotEqual = 0;

    /// <summary>
    /// Determine the points for the given Receiving PMode and UserMessage
    /// </summary>
    /// <param name="pmode"></param>
    /// <param name="userMessage"></param>
    /// <returns></returns>
    public int DeterminePoints(ReceivingProcessingMode pmode, UserMessage userMessage)
    {
        var pmodeCollaboration = pmode.MessagePackaging?.CollaborationInfo;
        var messageCollaboration = userMessage.CollaborationInfo;

        if (pmodeCollaboration == null)
        {
            return NotEqual;
        }

        var equalAction = StringComparer.OrdinalIgnoreCase
            .Equals(pmodeCollaboration.Action, messageCollaboration.Action);

        var noServiceType =
            pmodeCollaboration.Service?.Type == null
            && messageCollaboration.Service.Type == Maybe<string>.Nothing;

        var equalServiceType = messageCollaboration.Service.Type
            .Select(t => StringComparer.OrdinalIgnoreCase.Equals(pmodeCollaboration.Service?.Type, t))
            .GetOrElse(false);

        var equalServiceValue = StringComparer.OrdinalIgnoreCase
            .Equals(pmodeCollaboration?.Service?.Value, messageCollaboration.Service.Value);

        var equalService = equalServiceValue && (noServiceType || equalServiceType);

        return equalAction && equalService
            ? Points
            : NotEqual;
    }
}
