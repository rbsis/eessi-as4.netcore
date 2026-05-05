using System.Text;
using FluentValidation.Results;

namespace Eu.EDelivery.AS4.Validators;

public static class ValidationResultExtension
{
    public static IEnumerable<string> GetValidationErrors(this ValidationResult result)
    {
        foreach (var e in result.Errors)
        {
            yield return $"Validation Error: {e.PropertyName} = {e.ErrorMessage}";
        }
    }

    public static string AppendValidationErrorsToErrorMessage(this ValidationResult result, string errorMessage)
    {
        var sb = new StringBuilder(errorMessage);

        foreach (var validationError in result.GetValidationErrors())
        {
            sb.AppendLine(validationError);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Results the specified happy path.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="onValidationSuccess">The happy path.</param>
    /// <param name="onValidationFailed">The unhappy path.</param>
    /// <exception cref="Exception">A delegate callback throws an exception.</exception>
    public static void Result(this ValidationResult result, Action<ValidationResult> onValidationSuccess, Action<ValidationResult> onValidationFailed)
    {
        if (result.IsValid)
        {
            onValidationSuccess(result);
        }
        else
        {
            onValidationFailed(result);
        }
    }
}
