using System.Text;
using FluentValidation.Results;

namespace Eu.EDelivery.AS4.Fe.Pmodes;

/// <summary>
///     FluentValidation error collection extensions
/// </summary>
public static class ValidationErrorExtensions
{
    /// <summary>
    ///     Returns a <see cref="string" /> that represents this instance.
    /// </summary>
    /// <param name="failures">The failures.</param>
    /// <returns>
    ///     A <see cref="string" /> that represents this instance.
    /// </returns>
    public static string ToStringList(this IList<ValidationFailure> failures) => failures
        .Select(err => err.ErrorMessage)
        .Aggregate(new StringBuilder(), (builder, msg) => builder.AppendLine(msg))
        .ToString();
}
