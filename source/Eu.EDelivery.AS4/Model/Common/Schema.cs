using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace Eu.EDelivery.AS4.Model.Common;

public sealed class Schema : IEquatable<Schema>, IEqualityComparer<Schema>
{
    [XmlElement(IsNullable = true)]
    public string Location { get; set; }

    public string? Version { get; set; }

    public string? Namespace { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Schema"/> class. 
    /// Create a basic <see cref="Schema"/> Model
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public Schema() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    /// <summary>
    /// Initializes a new instance of the <see cref="Schema"/> class. 
    /// Create a <see cref="Schema"/> Model
    /// to a given <paramref name="location"/>
    /// </summary>
    /// <param name="location">
    /// </param>
    public Schema(string location)
    {
        Location = location;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <returns>
    /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
    /// </returns>
    /// <param name="other">An object to compare with this object.</param>
    public bool Equals(Schema? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return string.Equals(Location, other.Location, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Version, other.Version, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Namespace, other.Namespace, StringComparison.OrdinalIgnoreCase);
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

        return obj.GetType() == GetType() && Equals((Schema)obj);
    }

    /// <summary>Determines whether the specified object is equal to the current object.</summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public bool Equals(Schema? x, Schema? y) => x?.Equals(y) == true;

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
            var hashCode = Location != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Location) : 0;
            hashCode = (hashCode * 397) ^ (Version != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Version) : 0);
            hashCode = (hashCode * 397) ^ (Namespace != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Namespace) : 0);

            return hashCode;
        }
    }

    /// <summary>Serves as the default hash function. </summary>
    /// <param name="obj"></param>
    /// <returns>A hash code for the current object.</returns>
    public int GetHashCode([DisallowNull] Schema obj) => obj.GetHashCode();
}
