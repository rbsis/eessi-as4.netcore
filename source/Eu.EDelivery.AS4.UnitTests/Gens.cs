using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Mappings.Core;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.UnitTests.Model;
using Microsoft.FSharp.Core;

namespace Eu.EDelivery.AS4.UnitTests;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Extensibility", "xUnit3003:Classes which extend FactAttribute (directly or indirectly) should provide a public constructor for source information", Justification = "Done on PropertyAttribute constructor")]
public class CustomPropertyAttribute : PropertyAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomPropertyAttribute"/> class.
    /// </summary>
    public CustomPropertyAttribute()
    {
        Arbitrary = [typeof(Gens)];
    }
}

public class NonWhiteSpaceString
{
    internal NonWhiteSpaceString(NonEmptyString str)
    {
        Get = str.Get.Replace(" ", string.Empty);
    }

    public string Get { get; }
}

public static class Gens
{
    public static Arbitrary<NonWhiteSpaceString> NonWhiteSpaceString() => ArbMap.Default
        .GeneratorFor<NonEmptyString>()
        .Select(str => new NonWhiteSpaceString(str))
        .ToArbitrary();

    public static Arbitrary<MessageUnit> MessageUnits()
    {
        return Gen.Elements<MessageUnit>(
            new UserMessage($"user-{Guid.NewGuid()}"),
            new Receipt(
                $"receipt-{Guid.NewGuid()}",
                $"ref-to-user-{Guid.NewGuid()}"),
            new FilledNRReceipt(),
            new Error(
                $"error-{Guid.NewGuid()}",
                $"user-{Guid.NewGuid()}",
                AS4.Model.Core.ErrorLine.FromErrorResult(
                    new ErrorResult($"desc-{Guid.NewGuid()}", ErrorAlias.Other))))
                  .ToArbitrary();
    }

    public static Arbitrary<SignalMessage> SignalMessages() => MessageUnits()
        .Generator
        .Where(u => u is SignalMessage)
        .Select(u => (SignalMessage)u)
        .ToArbitrary();

    public static Arbitrary<Receipt> Receipt() => ArbMap.Default
        .GeneratorFor<NonEmptyString>()
        .Two()
        .Zip(GenNonRepudiation())
        .Zip(UserMessage().Generator)
        .Select(t => new Receipt(
            t.Item1.Item1.Item1.Get,
            t.Item1.Item1.Item2.Get,
            t.Item1.Item2,
            UserMessageMap.ConvertToRouting(t.Item2)))
        .ToArbitrary();

    private static Gen<NonRepudiationInformation> GenNonRepudiation()
    {
        var genDigestValue = ArbMap.Default.GeneratorFor<byte[]>();

        var genDigestMethod = ArbMap.Default
            .GeneratorFor<NonNull<string>>()
            .Select(x => new ReferenceDigestMethod(x.Get));

        var genTransforms = ArbMap.Default
            .GeneratorFor<NonNull<string>>()
            .Select(x => new ReferenceTransform(x.Get))
            .ListOf();

        return ArbMap.Default
            .GeneratorFor<NonNull<string>>()
            .Zip(genTransforms)
            .Zip(genDigestMethod.Zip(genDigestValue))
            .Select(t => new Reference(t.Item1.Item1.Get, t.Item1.Item2, t.Item2.Item1, t.Item2.Item2))
            .ListOf()
            .Select(rs => new NonRepudiationInformation(rs));
    }

    public static Arbitrary<Error> Error() => ArbMap.Default
        .GeneratorFor<NonEmptyString>()
        .Two()
        .Zip(UserMessage().Generator)
        .Zip(ErrorLine().ListOf())
        .Select(t => new Error(
            t.Item1.Item1.Item1.Get,
            t.Item1.Item1.Item2.Get,
            DateTimeOffset.Now,
            t.Item2,
            UserMessageMap.ConvertToRouting(t.Item1.Item2)))
        .ToArbitrary();

    private static Gen<ErrorLine> ErrorLine()
    {
        var genError = ArbMap.Default
            .GeneratorFor<Severity>()
            .Zip(ArbMap.Default.GeneratorFor<ErrorCode>())
            .Zip(ArbMap.Default.GeneratorFor<ErrorAlias>());

        return MaybeArbitrary<NonNull<string>>()
            .Generator
            .Four()
            .Zip(genError)
            .Zip(MaybeArbitrary<Tuple<NonNull<string>, NonNull<string>>>().Generator)
            .Select(t => new ErrorLine(
                t.Item1.Item2.Item1.Item2,
                t.Item1.Item2.Item1.Item1,
                t.Item1.Item2.Item2,
                t.Item1.Item1.Item1.Select(m => m.Get),
                t.Item1.Item1.Item2.Select(m => m.Get),
                t.Item1.Item1.Item3.Select(m => m.Get),
                t.Item2.Select(m => new ErrorDescription(m.Item1.Get, m.Item2.Get)),
                t.Item1.Item1.Item4.Select(m => m.Get)));
    }

    public static Arbitrary<UserMessage> UserMessage() => ArbMap.Default
        .GeneratorFor<NonEmptyString>()
        .Zip(GenCollaborationInfo())
        .Zip(GenParty().Two())
        .Zip(GenPartInfos().Zip(GenMessageProperties()))
        .Select(t => new UserMessage(
            t.Item1.Item1.Item1.Get,
            t.Item1.Item1.Item2,
            t.Item1.Item2.Item1,
            t.Item1.Item2.Item2,
            t.Item2.Item1,
            t.Item2.Item2))
        .ToArbitrary();

    private static Gen<CollaborationInfo> GenCollaborationInfo()
    {
        var genAgreementRef = ArbMap.Default
            .GeneratorFor<NonNull<string>>()
            .Select(x => new AgreementReference(x.Get).AsMaybe())
            .Or(Gen.Constant(Maybe<AgreementReference>.Nothing));

        var genServiceWithoutType = ArbMap.Default
            .GeneratorFor<NonNull<string>>()
            .Select(x => new Service(x.Get));

        var genServiceWithType = ArbMap.Default
            .GeneratorFor<NonNull<string>>()
            .Two()
            .Select(t => new Service(t.Item1.Get, t.Item2.Get));

        var genActionConversation = ArbMap.Default
            .GeneratorFor<NonNull<string>>()
            .Two();

        return genAgreementRef
            .Zip(genServiceWithoutType.Or(genServiceWithType))
            .Zip(genActionConversation)
            .Select(tt => new CollaborationInfo(
                tt.Item1.Item1,
                tt.Item1.Item2,
                tt.Item2.Item1.Get,
                tt.Item2.Item2.Get));
    }

    private static Gen<Party> GenParty() => ArbMap.Default
        .GeneratorFor<NonEmptyString>()
        .Zip(ArbMap.Default
            .GeneratorFor<NonEmptyString>()
            .NonEmptyListOf(),
            (role, ids) => new Party(
                role.Get,
                ids.Select(id => new PartyId(id.Get))));

    private static Gen<PartInfo[]> GenPartInfos()
    {
        var genSchemas = ArbMap.Default
            .GeneratorFor<NonNull<string>>()
            .Zip(Gens.MaybeArbitrary<NonNull<string>>().Generator.Select(t => t.Select(m => m.Get)))
            .Zip(Gens.MaybeArbitrary<NonNull<string>>().Generator.Select(t => t.Select(m => m.Get)))
            .Select(t => new Schema(t.Item1.Item1.Get, t.Item1.Item2, t.Item2))
            .ListOf();

        return ArbMap.Default
            .GeneratorFor<NonNull<string>>()
            .Zip(ArbMap.Default.GeneratorFor<IDictionary<string, string>>())
            .Zip(genSchemas)
            .Select(t => new PartInfo(t.Item1.Item1.Get, t.Item1.Item2, t.Item2))
            .ArrayOf();
    }

    private static Gen<MessageProperty[]> GenMessageProperties()
    {
        var genPropWithType = ArbMap.Default
            .GeneratorFor<NonEmptyString>()
            .Three()
            .Select(kv => new MessageProperty(kv.Item1.Get, kv.Item2.Get, kv.Item3.Get))
            .ArrayOf();

        var genPropWithoutType = ArbMap.Default
            .GeneratorFor<NonEmptyString>()
            .Two()
            .Select(kv => new MessageProperty(kv.Item1.Get, kv.Item2.Get))
            .ArrayOf();

        return genPropWithoutType.Or(genPropWithType);
    }

    public static Arbitrary<Maybe<T>> MaybeArbitrary<T>() => ArbMap.Default
        .GeneratorFor<FSharpOption<T>>()
        .Select(x => Equals(x, FSharpOption<T>.None)
            ? Maybe<T>.Nothing
            : Maybe.Just(x.Value))
        .ToArbitrary();
}
