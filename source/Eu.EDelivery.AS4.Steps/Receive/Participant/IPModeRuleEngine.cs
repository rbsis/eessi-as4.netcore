namespace Eu.EDelivery.AS4.Steps.Receive.Participant;

public interface IPModeRuleEngine
{
    PModeParticipant ApplyRules(PModeParticipant participant);
}
