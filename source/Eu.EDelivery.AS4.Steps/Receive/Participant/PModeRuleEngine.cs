using Eu.EDelivery.AS4.Steps.Receive.Rules;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Receive.Participant;

/// <summary>
/// Class to Provide <see cref="IPModeRule" /> implementations
/// </summary>
public class PModeRuleEngine : IPModeRuleEngine
{
    private readonly ILogger<PModeRuleEngine> _logger;

    public PModeRuleEngine(ILogger<PModeRuleEngine> logger)
    {
        _logger = logger;
    }

    private static readonly ICollection<IPModeRule> _rules =
    [
        new PModeIdRule(),
        new PModePartyInfoRule(),
        new PModeUndefinedPartyInfoRule(),
        new PModeAgreementRefRule(),
        new PModeServiceActionRule()
    ];

    /// <summary>
    /// Visits the <see cref="PModeParticipant" />:
    /// apply Rules on the Participant
    /// </summary>
    /// <param name="participant"></param>
    public PModeParticipant ApplyRules(PModeParticipant participant)
    {
        foreach (var rule in _rules)
        {
            var points = rule.DeterminePoints(participant.PMode, participant.UserMessage);
            _logger.LogTrace("PMode {PModeId}: {Points} Points determined for the {Rule}",
                participant.PMode.Id,
                points,
                rule.GetType().Name);

            participant.Points += points;
        }

        return participant;
    }
}
