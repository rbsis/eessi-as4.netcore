using System.Security.Cryptography.Xml;

namespace Eu.EDelivery.AS4.Model.Core;

public sealed class Reference : IEquatable<Reference>
{
    public string? URI { get; }

    public IEnumerable<ReferenceTransform> Transforms { get; }

    public ReferenceDigestMethod DigestMethod { get; }

    public byte[] DigestValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Reference"/> class.
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="transforms"></param>
    /// <param name="digestMethod"></param>
    /// <param name="digestValue"></param>
    public Reference(
        string? uri,
        IEnumerable<ReferenceTransform> transforms,
        ReferenceDigestMethod digestMethod,
        byte[] digestValue)
    {
        Transforms = transforms;
        DigestMethod = digestMethod;
        DigestValue = digestValue;
        URI = uri;
    }

    /// <summary>
    /// Creates a <see cref="Reference"/> model from a <see cref="System.Security.Cryptography.Xml.Reference"/> element.
    /// </summary>
    /// <param name="refElement"></param>
    /// <returns></returns>
    public static Reference CreateFromReferenceElement(System.Security.Cryptography.Xml.Reference refElement)
    {
        ArgumentNullException.ThrowIfNull(refElement);
        ArgumentNullException.ThrowIfNull(refElement.DigestValue);

        static IEnumerable<ReferenceTransform> CreateTransformsFromChain(TransformChain chain)
        {
            foreach (Transform transform in chain)
            {
                if (transform.Algorithm != null)
                {
                    yield return new ReferenceTransform(transform.Algorithm);
                }
            }
        }

        return new(
            refElement.Uri,
            CreateTransformsFromChain(refElement.TransformChain),
            new ReferenceDigestMethod(refElement.DigestMethod),
            refElement.DigestValue);
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
    public bool Equals(Reference? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(URI, other.URI)
            && Transforms.SequenceEqual(other.Transforms)
            && DigestMethod.Equals(other.DigestMethod)
            && DigestValue.Equals(other.DigestValue);
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

        return obj is Reference r && Equals(r);
    }

    /// <summary>Serves as the default hash function. </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(URI, Transforms, DigestMethod, DigestValue);
    }

    /// <summary>
    /// Returns a value that indicates whether the values of two <see cref="T:Eu.EDelivery.AS4.Model.Core.Reference" /> objects are equal.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
    public static bool operator ==(Reference left, Reference right) => Equals(left, right);

    /// <summary>
    /// Returns a value that indicates whether two <see cref="T:Eu.EDelivery.AS4.Model.Core.Reference" /> objects have different values.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
    public static bool operator !=(Reference left, Reference right) => !Equals(left, right);
}
