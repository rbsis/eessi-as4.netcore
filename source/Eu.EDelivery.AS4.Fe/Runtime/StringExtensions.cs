namespace Eu.EDelivery.AS4.Fe.Runtime;

public static class StringExtensions
{
    public static string? ToCamelCase(this string inputString)
    {
        // If there are 0 or 1 characters, just return the string.
        if (inputString == null || inputString.Length < 2)
            return inputString;

        // Split the string into words.
        var words = inputString.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);

        // Combine the words.
        var result = words[0].ToLower();
        for (var i = 1; i < words.Length; i++)
        {
            result += string.Concat(words[i][..1].ToUpper(), words[i].AsSpan(1));
        }

        return result;
    }
}
