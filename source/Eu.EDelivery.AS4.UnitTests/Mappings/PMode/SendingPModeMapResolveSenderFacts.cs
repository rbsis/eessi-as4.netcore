using Eu.EDelivery.AS4.Mappings.PMode;
using Eu.EDelivery.AS4.Model.PMode;
using CoreParty = Eu.EDelivery.AS4.Model.Core.Party;

namespace Eu.EDelivery.AS4.UnitTests.Mappings.PMode;

public class SendingPModeMapResolveSenderFacts
{
    [Property]
    public Property ThenSenderGetsPopulatedWhenPresent(
        NonEmptyString role,
        NonEmptyString partyId)
    {
        return Prop.ForAll(
            ArbParty(role, partyId),
            p =>
            {
                var actual = SendingPModeMap.ResolveSender(p);

                var isDefault = actual.Equals(CoreParty.DefaultFrom);
                var isResolved = actual.Role == role.Get && actual.PrimaryPartyId == partyId.Get;

                return (isDefault == (p == null)).And(isResolved == (p != null));
            });
    }

    [Property]
    public Property ThenReceiverGetsPopulatedWhenPresent(
        NonEmptyString role,
        NonEmptyString partyId)
    {
        return Prop.ForAll(
            ArbParty(role, partyId),
            p =>
            {
                var actual = SendingPModeMap.ResolveReceiver(p);

                var isDefault = actual.Equals(CoreParty.DefaultTo);
                var isResolved = actual.Role == role.Get && actual.PrimaryPartyId == partyId.Get;

                return (isDefault == (p == null)).And(isResolved == (p != null));
            });
    }

    private static Arbitrary<Party?> ArbParty(NonEmptyString role, NonEmptyString partyId)
    {
        return Gen.OneOf(
            Gen.Fresh<Party?>(() => new Party(role.Get, partyId.Get)),
            Gen.Constant<Party?>(null))
                  .ToArbitrary();
    }
}
