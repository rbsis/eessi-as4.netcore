using System.Diagnostics.CodeAnalysis;

namespace Eu.EDelivery.AS4.Model.Common;

public sealed class MessageInfo : IEquatable<MessageInfo>, IEqualityComparer<MessageInfo>
{
    public string? MessageId { get; set; }
    public string? RefToMessageId { get; set; }
    public string? Mpc { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageInfo"/> class. 
    /// Xml Serializer needs empty constructor
    /// </summary>
    public MessageInfo() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageInfo"/> class.
    /// </summary>
    public MessageInfo(string messageId)
    {
        MessageId = messageId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageInfo"/> class. 
    /// Create a <see cref="MessageInfo"/> Model
    /// with a given <paramref name="messageId"/> and <paramref name="mpc"/>
    /// </summary>
    /// <param name="messageId"></param>
    /// <param name="mpc"></param>
    public MessageInfo(string messageId, string mpc)
    {
        MessageId = messageId;
        Mpc = mpc;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageInfo"/> class. 
    /// Create a <see cref="MessageInfo"/> Model
    /// with a given <paramref name="messageId"/> and <paramref name="mpc"/>
    /// </summary>
    /// <param name="messageId"></param>
    /// <param name="refToMessageId"></param>
    /// <param name="mpc"></param>
    public MessageInfo(string messageId, string refToMessageId, string mpc)
    {
        MessageId = messageId;
        RefToMessageId = refToMessageId;
        Mpc = mpc;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <returns>
    /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
    /// </returns>
    /// <param name="other">An object to compare with this object.</param>
    public bool Equals(MessageInfo? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return
            string.Equals(MessageId, other.MessageId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(RefToMessageId, other.RefToMessageId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Mpc, other.Mpc, StringComparison.OrdinalIgnoreCase);
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
        if (obj is null)
        {
            return false;
        }
        if (ReferenceEquals(this, obj))
        {
            return true;
        }
        return obj.GetType() == GetType() && Equals((MessageInfo)obj);
    }

    /// <summary>Determines whether the specified object is equal to the current object.</summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public bool Equals(MessageInfo? x, MessageInfo? y) => x?.Equals(y) == true;

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
            var hashCode = MessageId != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(MessageId) : 0;
            hashCode = (hashCode * 397)
                       ^ (RefToMessageId != null
                              ? StringComparer.OrdinalIgnoreCase.GetHashCode(RefToMessageId)
                              : 0);
            hashCode = (hashCode * 397)
                       ^ (Mpc != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Mpc) : 0);
            return hashCode;
        }
    }

    /// <summary>Serves as the default hash function. </summary>
    /// <param name="obj"></param>
    /// <returns>A hash code for the current object.</returns>
    public int GetHashCode([DisallowNull] MessageInfo obj) => obj.GetHashCode();
}
