using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace Eu.EDelivery.AS4.Model.Common;

public sealed class PayloadProperty : IEquatable<PayloadProperty>, IEqualityComparer<PayloadProperty>
{
    [XmlElement(IsNullable = true)]
    public string? Name { get; set; }

    public string? Value { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadProperty"/> class
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public PayloadProperty() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadProperty"/> class
    /// with a given <paramref name="name"/>
    /// </summary>
    /// <param name="name"></param>
    public PayloadProperty(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadProperty"/> class.
    /// </summary>
    public PayloadProperty(string name, string? value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <returns>
    /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
    /// </returns>
    /// <param name="other">An object to compare with this object.</param>
    public bool Equals(PayloadProperty? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return
            string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
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

        return obj.GetType() == GetType() && Equals((PayloadProperty)obj);
    }

    /// <summary>Determines whether the specified object is equal to the current object.</summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public bool Equals(PayloadProperty? x, PayloadProperty? y) => x?.Equals(y) == true;

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
            return ((Name != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Name) : 0) * 397)
                   ^ (Value != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Value) : 0);
        }
    }

    /// <summary>Serves as the default hash function. </summary>
    /// <param name="obj"></param>
    /// <returns>A hash code for the current object.</returns>
    public int GetHashCode([DisallowNull] PayloadProperty obj) => obj.GetHashCode();
}
