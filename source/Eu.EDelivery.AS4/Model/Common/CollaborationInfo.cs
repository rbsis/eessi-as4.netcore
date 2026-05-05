using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace Eu.EDelivery.AS4.Model.Common;

public sealed class CollaborationInfo : IEquatable<CollaborationInfo>, IEqualityComparer<CollaborationInfo>
{
    public string? Action { get; set; }

    public string? ConversationId { get; set; }

    [XmlElement(IsNullable = true)]
    public Agreement? AgreementRef { get; set; }

    public Service? Service { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CollaborationInfo"/> class. 
    /// Create a basic <see cref="CollaborationInfo"/> Model
    /// </summary>
    public CollaborationInfo()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CollaborationInfo"/> class. 
    /// Create a <see cref="CollaborationInfo"/> Model
    /// with a given <paramref name="conversationId"/> 
    /// and <paramref name="agreement"/>
    /// </summary>
    /// <param name="conversationId">
    /// </param>
    /// <param name="agreement">
    /// </param>
    public CollaborationInfo(string conversationId, Agreement agreement)
    {
        ConversationId = conversationId;
        AgreementRef = agreement;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CollaborationInfo"/> class. 
    /// Create a <see cref="CollaborationInfo"/> Model
    /// with a given <paramref name="conversationId"/>, <paramref name="agreement"/> and <paramref name="service"/>
    /// </summary>
    /// <param name="conversationId">
    /// </param>
    /// <param name="agreement">
    /// </param>
    /// <param name="service">
    /// </param>
    public CollaborationInfo(string conversationId, Agreement agreement, Service service)
    {
        ConversationId = conversationId;
        AgreementRef = agreement;
        Service = service;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <returns>
    /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
    /// </returns>
    /// <param name="other">An object to compare with this object.</param>
    public bool Equals(CollaborationInfo? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return
            string.Equals(Action, other.Action, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(ConversationId, other.ConversationId, StringComparison.OrdinalIgnoreCase) &&
            Equals(AgreementRef, other.AgreementRef) && Equals(Service, other.Service);
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
        return obj.GetType() == GetType() && Equals((CollaborationInfo)obj);
    }

    /// <summary>Determines whether the specified object is equal to the current object.</summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public bool Equals(CollaborationInfo? x, CollaborationInfo? y) => x?.Equals(y) == true;

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
            var hashCode = Action != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Action) : 0;
            hashCode = (hashCode * 397)
                       ^ (ConversationId != null
                              ? StringComparer.OrdinalIgnoreCase.GetHashCode(ConversationId)
                           : 0);
            hashCode = (hashCode * 397) ^ (AgreementRef?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (Service?.GetHashCode() ?? 0);
            return hashCode;
        }
    }

    /// <summary>Serves as the default hash function. </summary>
    /// <param name="obj"></param>
    /// <returns>A hash code for the current object.</returns>
    public int GetHashCode([DisallowNull] CollaborationInfo obj) => obj.GetHashCode();
}
