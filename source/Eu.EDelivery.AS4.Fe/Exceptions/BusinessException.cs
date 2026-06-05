namespace Eu.EDelivery.AS4.Fe.Exceptions;

/// <summary>
/// Exception class to carry business exceptions
/// </summary>
/// <seealso cref="Exception" />
public class BusinessException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BusinessException(string message) : base(message)
    {
    }
}
