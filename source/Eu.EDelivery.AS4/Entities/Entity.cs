using System.Diagnostics.CodeAnalysis;

namespace Eu.EDelivery.AS4.Entities;

public class Entity : IEquatable<Entity>, IEqualityComparer<Entity>
{
    public long Id { get; private set; }

    public DateTimeOffset InsertionTime { get; set; }

    public DateTimeOffset ModificationTime { get; set; }

    public void InitializeIdFromDatabase(long id)
    {
        Id = id;
    }

    public bool IsTransient => Id == default;

    /// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
    /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
    /// <param name="other">An object to compare with this object.</param>
    public virtual bool Equals(Entity? other)
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

    /// <summary>Determines whether the specified object is equal to the current object.</summary>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    /// <param name="obj">The object to compare with the current object. </param>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
        {
            return false;
        }

        if (other.GetType() != GetType())
        {
            return false;
        }

        return Equals(other);
    }

    /// <summary>Determines whether the specified object is equal to the current object.</summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public bool Equals(Entity? x, Entity? y) => x?.Equals(y) == true;

    /// <summary>Serves as the default hash function. </summary>
    /// <returns>A hash code for the current object.</returns>
    [SuppressMessage("Major Bug", "S3249:Classes directly extending \"object\" should not call \"base\" in \"GetHashCode\" or \"Equals\"", Justification = "<Pending>")]
    public override int GetHashCode() => IsTransient ? base.GetHashCode() : Id.GetHashCode();

    /// <summary>Serves as the default hash function. </summary>
    /// <param name="obj"></param>
    /// <returns>A hash code for the current object.</returns>
    public int GetHashCode([DisallowNull] Entity obj) => obj.GetHashCode();
}
