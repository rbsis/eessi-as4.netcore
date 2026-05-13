using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;

namespace Eu.EDelivery.AS4.Steps.Receive.Rules;

/// <summary>
/// PMode Rule to check if the PMode Agreement Ref is equal to the UserMessage Agreement Ref
/// </summary>
internal class PModeAgreementRefRule : IPModeRule
{
    private const int Points = 4;
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

        var pmodeAgreement =
            pmode.MessagePackaging
                 ?.CollaborationInfo
                 ?.AgreementReference;

        var userAgreement =
            userMessage.CollaborationInfo
                       .AgreementReference
                       .GetOrElse(() => null!);

        if (pmodeAgreement is null || userAgreement is null)
        {
            return NotEqual;
        }

        var equalPModeId =
            userAgreement.PModeId
               .Select(id => StringComparer.OrdinalIgnoreCase.Equals(id, pmodeAgreement?.PModeId))
               .GetOrElse(false);

        var noPModeId =
            userAgreement.PModeId == Maybe<string>.Nothing
            && pmodeAgreement?.PModeId == null;

        var equalType =
            userAgreement.Type
               .Select(t => StringComparer.OrdinalIgnoreCase.Equals(t, pmodeAgreement?.Type))
               .GetOrElse(false);

        var noType =
            userAgreement.Type == Maybe<string>.Nothing
            && pmodeAgreement?.Type == null;

        var equalValue =
            StringComparer
                .OrdinalIgnoreCase
                .Equals(userAgreement.Value, pmodeAgreement?.Value);

        return (equalPModeId || noPModeId)
               && (equalType || noType)
               && equalValue
            ? Points
            : NotEqual;
    }
}
