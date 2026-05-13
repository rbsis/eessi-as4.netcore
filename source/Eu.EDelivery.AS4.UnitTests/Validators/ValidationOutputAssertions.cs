using Eu.EDelivery.AS4.Model.PMode;

namespace Eu.EDelivery.AS4.UnitTests.Validators;

public static class ValidationOutputAssertions
{
    public static bool SpecifiedMethod(Method? m)
    {
        var specifiedType = !string.IsNullOrWhiteSpace(m?.Type);
        var specifiedParams =
            m?.Parameters?.All(p => !string.IsNullOrWhiteSpace(p?.Name)
                                    && !string.IsNullOrWhiteSpace(p?.Value))
            ?? false;

        return specifiedType && specifiedParams;
    }
}
