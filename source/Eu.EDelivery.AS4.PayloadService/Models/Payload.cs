namespace Eu.EDelivery.AS4.PayloadService.Models;

/// <summary>
/// Model to define the structure of how a uploaded payload looks like.
/// </summary>
public sealed class Payload : IEquatable<Payload>, IDisposable
{
    private bool _disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="Payload"/> class.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="payloadMeta">The payload Meta.</param>
    public Payload(Stream content, PayloadMeta payloadMeta)
    {
        Content = content;
        Meta = payloadMeta;
    }

    /// <summary>
    /// Gets the content of the <see cref="Payload"/>.
    /// </summary>
    public Stream Content { get; }

    /// <summary>
    /// Gets the metadata of the <see cref="Payload"/>.
    /// </summary>
    public PayloadMeta Meta { get; }

    /// <summary>
    /// Null Object of the <see cref="Payload"/> model.
    /// </summary>
    public static Payload Null => new(Stream.Null, new(string.Empty));

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
    /// <param name="other">An object to compare with this object.</param>
    public bool Equals(Payload? other)
    {
        return Meta.OriginalFileName.Equals(other?.Meta.OriginalFileName);
    }

    public override bool Equals(object? obj) => Equals(obj as Payload);

    public override int GetHashCode() => Meta.OriginalFileName.GetHashCode();

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                Content?.Dispose();
            }

            _disposedValue = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
