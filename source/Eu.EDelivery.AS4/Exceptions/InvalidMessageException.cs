namespace Eu.EDelivery.AS4.Exceptions;

[Serializable]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3925:\"ISerializable\" should be implemented correctly", Justification = "<Pending>")]
public class InvalidMessageException : Exception
{
    public InvalidMessageException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidMessageException"/> class.
    /// </summary>
    public InvalidMessageException(string message)
        : base(message)
    {
    }

    public InvalidMessageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
