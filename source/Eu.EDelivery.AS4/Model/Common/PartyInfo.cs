using System.Diagnostics.CodeAnalysis;

namespace Eu.EDelivery.AS4.Model.Common;

public sealed class PartyInfo : IEquatable<PartyInfo>, IEqualityComparer<PartyInfo>
{
    public Party? FromParty { get; set; }
    public Party? ToParty { get; set; }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <returns>
    /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
    /// </returns>
    /// <param name="other">An object to compare with this object.</param>
    public bool Equals(PartyInfo? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Equals(FromParty, other.FromParty) && Equals(ToParty, other.ToParty);
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

        return obj.GetType() == GetType() && Equals((PartyInfo)obj);
    }

    /// <summary>Determines whether the specified object is equal to the current object.</summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public bool Equals(PartyInfo? x, PartyInfo? y) => x?.Equals(y) == true;

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
            return ((FromParty?.GetHashCode() ?? 0) * 397) ^ (ToParty?.GetHashCode() ?? 0);
        }
    }

    /// <summary>Serves as the default hash function. </summary>
    /// <param name="obj"></param>
    /// <returns>A hash code for the current object.</returns>
    public int GetHashCode([DisallowNull] PartyInfo obj) => obj.GetHashCode();
}
