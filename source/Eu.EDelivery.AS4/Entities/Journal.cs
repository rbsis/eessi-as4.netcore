using System.ComponentModel.DataAnnotations;

namespace Eu.EDelivery.AS4.Entities;

public sealed class Journal : IEquatable<Journal>
{
    public long Id { get; private set; }

    public void InitializeIdFromDatabase(long id)
    {
        Id = id;
    }

    public bool IsTransient => Id == default;

    public long? RefToInMessageId { set; get; }

    public long? RefToOutMessageId { get; set; }

    public DateTimeOffset LogDate { get; set; }

    [Required]
    [MaxLength(20)]
    public required string AgentType { get; set; }

    [Required]
    [MaxLength(50)]
    public required string AgentName { get; set; }

    [Required]
    [MaxLength(100)]
    public required string EbmsMessageId { get; set; }

    [MaxLength(100)]
    public string? RefToEbmsMessageId { get; set; }

    [MaxLength(255)]
    public string? FromParty { get; set; }

    [MaxLength(255)]
    public string? ToParty { get; set; }

    [MaxLength(255)]
    public string? Service { get; set; }

    [MaxLength(255)]
    public string? Action { get; set; }

    [Required]
    public required string LogEntry { get; set; }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
    /// <param name="other">An object to compare with this object.</param>
    public bool Equals(Journal? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Id == other.Id;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
    /// <param name="obj">The object to compare with the current object. </param>
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is Journal j && Equals(j);
    }


    /// <summary>
    /// Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Bug", "S3249:Classes directly extending \"object\" should not call \"base\" in \"GetHashCode\" or \"Equals\"", Justification = "<Pending>")]
    public override int GetHashCode() => IsTransient ? base.GetHashCode() : Id.GetHashCode();
}
