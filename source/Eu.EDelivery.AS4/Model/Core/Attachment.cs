using Eu.EDelivery.AS4.Compression;
using Eu.EDelivery.AS4.Streaming;

namespace Eu.EDelivery.AS4.Model.Core;

public class Attachment : IEquatable<Attachment>
{
    /// <summary>
    /// Identifier of the <see cref="Attachment"/> to distinguish between entities.
    /// </summary>
    public string Id { get; }

    public string ContentType { get; private set; }

    public Stream Content { get; private set; }

    public long EstimatedContentSize { get; private set; }

    /// <summary>
    /// Updates both the content and the type of the content in the <see cref="Attachment"/>.
    /// </summary>
    /// <param name="content"></param>
    /// <param name="contentType"></param>
    public void UpdateContent(Stream content, string contentType)
    {
        if (!ReferenceEquals(Content, content))
        {
            Content?.Dispose();
        }

        Content = content;
        ContentType = contentType;

        EstimatedContentSize = Content != null ? Content.GetStreamSize() : -1;
    }

    public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indication whether or not this attachment has a MimeType property configured.
    /// </summary>
    public bool HasMimeType => Properties.ContainsKey("MimeType");

    /// <summary>
    /// Gets or sets the MimeType property of this attachment.
    /// </summary>
    public string MimeType
    {
        get => Properties["MimeType"];
        set => Properties["MimeType"] = value;
    }

    /// <summary>
    /// Gets or sets the type of the compression property of this attachment.
    /// </summary>
    public string CompressionType
    {
        get => Properties["CompressionType"];
        set => Properties["CompressionType"] = value;
    }

    /// <summary>
    /// Gets a value indicating whether or not this attachment is compressed.
    /// </summary>
    public bool IsCompressed =>
        ContentType.Equals(CompressStrategy.CompressionType, StringComparison.OrdinalIgnoreCase)
        || Properties.ContainsKey("CompressionType");

    /// <summary>
    /// Initializes a new instance of the <see cref="Attachment"/> class.
    /// </summary>
    /// <param name="id"></param>
    public Attachment(string id) : this(id, Stream.Null, "application/octet-stream") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Attachment"/> class.
    /// </summary>
    /// <param name="content"></param>
    /// <param name="contentType"></param>
    public Attachment(Stream content, string contentType)
        : this(Guid.NewGuid().ToString(), content, contentType) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Attachment"/> class.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="content"></param>
    /// <param name="contentType"></param>
    public Attachment(
        string id,
        Stream content,
        string contentType)
    {
        Id = id.Replace(" ", string.Empty);
        Content = content;
        ContentType = contentType;
        EstimatedContentSize = content.GetStreamSize();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Attachment"/> class.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="content"></param>
    /// <param name="contentType"></param>
    /// <param name="props"></param>
    public Attachment(
        string id,
        Stream content,
        string contentType,
        IDictionary<string, string> props)
    {
        Id = id.Replace(" ", string.Empty);
        Content = content;
        ContentType = contentType;
        Properties = props;
        EstimatedContentSize = content.GetStreamSize();
    }

    /// <summary>
    /// Verifies if this is the Attachment that is referenced by the given <paramref name="payload"/>.
    /// </summary>
    /// <param name="payload"></param>
    /// <returns></returns>
    public bool Matches(Common.Payload payload) =>
        payload != null && payload.Id != null && payload.Id.Equals($"cid:{Id}");

    /// <summary>
    /// Verifies if this is the Attachment that is referenced by the given <paramref name="partInfo"/>
    /// </summary>
    /// <param name="partInfo"></param>
    /// <returns></returns>
    public bool Matches(PartInfo? partInfo) =>
        partInfo is not null && partInfo.Href != null && partInfo.Href.Equals($"cid:{Id}");

    /// <summary>
    /// Verifies if this is the Attachment that is referenced by the given cryptography <paramref name="reference"/>
    /// </summary>
    /// <param name="reference"></param>
    /// <returns></returns>
    public bool Matches(System.Security.Cryptography.Xml.Reference? reference) =>
        reference != null && reference.Uri != null && reference.Uri.Equals($"cid:{Id}");

    /// <summary>
    /// Verifies if this Attachment is referred by any of the specified <paramref name="partInfos"/>
    /// </summary>
    /// <param name="partInfos"></param>
    /// <returns></returns>
    public bool MatchesAny(IEnumerable<PartInfo> partInfos) =>
        partInfos != null && partInfos.Any(Matches);

    /// <summary>
    /// Makes sure that the Attachment Content is positioned at the start of the content.
    /// </summary>
    public void ResetContentPosition() => Content?.MovePositionToStreamStart();

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
    public virtual bool Equals(Attachment? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(Id, other.Id);
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

        return obj is Attachment a && Equals(a);
    }

    /// <summary>
    /// Serves as the default hash function. 
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode() => Id.GetHashCode();

    /// <summary>
    /// Returns a value that indicates whether the values of two <see cref="T:Eu.EDelivery.AS4.Model.Core.Attachment" /> objects are equal.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
    public static bool operator ==(Attachment left, Attachment right) => Equals(left, right);

    /// <summary>
    /// Returns a value that indicates whether two <see cref="T:Eu.EDelivery.AS4.Model.Core.Attachment" /> objects have different values.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
    public static bool operator !=(Attachment left, Attachment right) => !Equals(left, right);
}
