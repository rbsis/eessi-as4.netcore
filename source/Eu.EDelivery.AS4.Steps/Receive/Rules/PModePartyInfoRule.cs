using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;

namespace Eu.EDelivery.AS4.Steps.Receive.Rules;

/// <summary>
/// PMode Rule to check if the PMode Parties are equal to the UserMessage Parties
/// </summary>
internal class PModePartyInfoRule : IPModeRule
{
    private const int ToPartyPoints = 8;
    private const int FromPartyPoints = 7;
    private const int PartyRolePoints = 1;
    private const int NotEqual = 0;

    /// <summary>
    /// Determine the points for the given Receiving PMode and UserMessage
    /// </summary>
    /// <param name="pmode"></param>
    /// <param name="userMessage"></param>
    /// <returns></returns>
    public int DeterminePoints(ReceivingProcessingMode pmode, UserMessage userMessage)
    {
        ArgumentNullException.ThrowIfNull(pmode);

        ArgumentNullException.ThrowIfNull(userMessage);

        var pmodePartyInfo = pmode.MessagePackaging?.PartyInfo;
        if (pmodePartyInfo == null)
        {
            return NotEqual;
        }

        if (!pmodePartyInfo.FromPartySpecified
            && !pmodePartyInfo.ToPartySpecified)
        {
            return NotEqual;
        }

        var points = 0;

        var fromPartyEqual = ArePartyIdsEqual(pmodePartyInfo.FromParty, userMessage.Sender);
        var toPartyEqual = ArePartyIdsEqual(pmodePartyInfo.ToParty, userMessage.Receiver);

        if (fromPartyEqual && !pmodePartyInfo.ToPartySpecified)
        {
            points += FromPartyPoints;
        }

        if (toPartyEqual && !pmodePartyInfo.FromPartySpecified)
        {
            points += ToPartyPoints;
        }

        if (fromPartyEqual && toPartyEqual)
        {
            points += FromPartyPoints + ToPartyPoints;
        }

        if (ArePartyRolesEqual(pmodePartyInfo, userMessage))
        {
            points += PartyRolePoints;
        }

        return points;
    }

    private static bool ArePartyIdsEqual(
        Model.PMode.Party? pmodeParty,
        Model.Core.Party messageParty)
    {
        if (pmodeParty == null || pmodeParty.PartyIds == null)
        {
            return false;
        }

        return messageParty.PartyIds.All(userPartyId => pmodeParty.PartyIds.Any(pmodePartyId =>
        {
            var noType =
                userPartyId.Type == Maybe<string>.Nothing
                && pmodePartyId?.Type == null;

            var equalType =
                userPartyId
                    .Type
                    .Select(t => StringComparer.OrdinalIgnoreCase.Equals(t, pmodePartyId?.Type))
                    .GetOrElse(false);

            var equalId =
                StringComparer
                    .OrdinalIgnoreCase
                    .Equals(userPartyId.Id, pmodePartyId?.Id);

            return equalId && (equalType || noType);
        }));
    }

    private static bool ArePartyRolesEqual(PartyInfo pmodePartyInfo, UserMessage userMessage)
    {
        if (pmodePartyInfo?.FromParty == null
            || pmodePartyInfo?.ToParty == null)
        {
            return false;
        }

        var equalFromRoles =
            StringComparer
                .OrdinalIgnoreCase
                .Equals(pmodePartyInfo.FromParty.Role, userMessage.Sender.Role);

        var equalToRoles =
            StringComparer
                .OrdinalIgnoreCase
                .Equals(pmodePartyInfo.ToParty.Role, userMessage.Receiver.Role);

        return equalFromRoles && equalToRoles;
    }

}
