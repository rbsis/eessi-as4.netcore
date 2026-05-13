namespace Eu.EDelivery.AS4.Model.Core;

public sealed class NonRepudiationInformation : IEquatable<NonRepudiationInformation>
{
    // ReSharper disable once InconsistentNaming
    public IEnumerable<Reference> MessagePartNRIReferences { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NonRepudiationInformation"/> class.
    /// </summary>
    public NonRepudiationInformation(IEnumerable<Reference> nriReferences)
    {
        if (nriReferences == null || nriReferences.Any(r => r is null))
        {
            throw new ArgumentNullException(nameof(nriReferences));
        }

        MessagePartNRIReferences = nriReferences;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
    public bool Equals(NonRepudiationInformation? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return MessagePartNRIReferences.SequenceEqual(other.MessagePartNRIReferences);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object. </param>
    /// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
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

        return obj is NonRepudiationInformation nrr && Equals(nrr);
    }

    /// <summary>
    /// Serves as the default hash function. 
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode() => MessagePartNRIReferences.GetHashCode();

    /// <summary>
    /// Returns a value that indicates whether the values of two <see cref="T:Eu.EDelivery.AS4.Model.Core.NonRepudiationInformation" /> objects are equal.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
    public static bool operator ==(NonRepudiationInformation left, NonRepudiationInformation right) => Equals(left, right);

    /// <summary>
    /// Returns a value that indicates whether two <see cref="T:Eu.EDelivery.AS4.Model.Core.NonRepudiationInformation" /> objects have different values.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
    public static bool operator !=(NonRepudiationInformation left, NonRepudiationInformation right) => !Equals(left, right);
}
