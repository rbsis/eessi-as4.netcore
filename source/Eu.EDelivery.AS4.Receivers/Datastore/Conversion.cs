using System.ComponentModel;

namespace Eu.EDelivery.AS4.Receivers.Datastore;

internal static class Conversion
{
    /// <summary>
    /// The convert.
    /// </summary>
    /// <param name="targetType">The property.</param>
    /// <param name="value">The value.</param>
    public static object? Convert(Type targetType, string? value)
    {
        if (value == null || value.Equals("NULL"))
        {
            return null;
        }

        return TypeDescriptor.GetConverter(targetType).ConvertFrom(value);
    }
}
