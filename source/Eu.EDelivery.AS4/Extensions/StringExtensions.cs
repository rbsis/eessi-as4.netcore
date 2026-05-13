namespace Eu.EDelivery.AS4.Extensions;

public static class StringExtensions
{
    public static TimeSpan AsTimeSpan(this string source)
    {
        return AsTimeSpan(source, default);
    }

    public static TimeSpan AsTimeSpan(this string source, TimeSpan defaulTimeSpan)
    {
        if (!string.IsNullOrWhiteSpace(source))
        {
            var isParsedCorrectly = TimeSpan.TryParse(source, out var resultedTimeSpan);

            if (isParsedCorrectly)
            {
                return resultedTimeSpan;
            }
        }

        return defaulTimeSpan;
    }

    public static T ToEnum<T>(this string? x, T defaultValue = default) where T : struct, IConvertible
    {
        return x != null && Enum.TryParse(x, ignoreCase: true, result: out T output)
            ? output
            : defaultValue;
    }
}
