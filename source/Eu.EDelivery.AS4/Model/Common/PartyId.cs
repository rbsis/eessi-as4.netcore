using System.Diagnostics.CodeAnalysis;

namespace Eu.EDelivery.AS4.Model.Common;

public sealed class PartyId : IEquatable<PartyId>, IEqualityComparer<PartyId>
{
    public string Id { get; set; }
    public string? Type { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyId"/> class. 
    /// Xml Serializer needs a parameter less constructor
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public PartyId() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyId"/> class. 
    /// Create a <see cref="PartyId"/> Model
    /// with a given <paramref name="id"/>
    /// </summary>
    /// <param name="id">
    /// </param>
    public PartyId(string id)
    {
        Id = id;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyId"/> class. 
    /// Create a <see cref="PartyId"/> Model
    /// with a given <paramref name="id"/> and <paramref name="type"/>
    /// </summary>
    /// <param name="id">
    /// </param>
    /// <param name="type">
    /// </param>
    public PartyId(string id, string? type)
    {
        Id = id;
        Type = type;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <returns>
    /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
    /// </returns>
    /// <param name="other">An object to compare with this object.</param>
    public bool Equals(PartyId? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return
            string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Type ?? string.Empty, other.Type ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <returns>
    /// true if the specified object  is equal to the current object; otherwise, false.
    /// </returns>
    /// <param name="obj">The object to compare with the current object. </param>
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;

        if (obj is not PartyId other)
        {
            return false;
        }

        return Equals(other);
    }

    /// <summary>Determines whether the specified object is equal to the current object.</summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public bool Equals(PartyId? x, PartyId? y) => x?.Equals(y) == true;

    /// <summary>
    /// Serves as the default hash function.
    /// </summary>
    /// <returns>
    /// A hash code for the current object.
    /// </returns>
    public override int GetHashCode()
    {
        unchecked
        {
            return ((Id != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Id) : 0) * 397)
                   ^ (Type != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Type) : 0);
        }
    }


    /// <summary>Serves as the default hash function. </summary>
    /// <param name="obj"></param>
    /// <returns>A hash code for the current object.</returns>
    public int GetHashCode([DisallowNull] PartyId obj) => obj.GetHashCode();
}
