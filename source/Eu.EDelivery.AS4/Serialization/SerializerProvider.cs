using Eu.EDelivery.AS4.Model.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Eu.EDelivery.AS4.Serialization;

/// <summary>
/// Class to provide <see cref="ISerializer"/> implementations
/// </summary>
public class SerializerProvider : ISerializerProvider
{
    private readonly IDictionary<string, ISerializer> _serializers;

    public SerializerProvider(
        [FromKeyedServices(Constants.ContentTypes.Soap)] ISerializer soapSerializer,
        [FromKeyedServices(Constants.ContentTypes.Mime)] ISerializer mimeSerializer)
    {
        _serializers = new Dictionary<string, ISerializer>
        {
            [Constants.ContentTypes.Soap] = soapSerializer,
            [Constants.ContentTypes.Mime] = mimeSerializer
        };
    }

    /// <summary>
    /// Get the <see cref="ISerializer"/> implementation
    /// based on a given Content Type
    /// </summary>
    /// <param name="contentType"></param>
    /// <returns></returns>
    public ISerializer Get(string contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        return _serializers.Where(p => KeyMatchesContentType(contentType, p.Key))
            .Select(p => p.Value)
            .FirstOrDefault()
            ?? throw new KeyNotFoundException($"No given Serializer found for a given Content Type: {contentType}");
    }

    /// <summary>
    /// Determines the size of an <see cref="AS4Message"/>
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public long DetermineMessageSize(AS4Message? message)
    {
        if (message == null)
        {
            return 0L;
        }

        var serializer = Get(message.ContentType);

        using var stream = new DetermineSizeStream();
        serializer.Serialize(message, stream);

        return stream.Length;
    }

    private static bool KeyMatchesContentType(string contentType, string key) =>
        key.Equals(contentType) || contentType.StartsWith(key, StringComparison.OrdinalIgnoreCase);

    #region Inner DetermineSizeStream class.

    private sealed class DetermineSizeStream : Stream
    {
        private long _length;

        /// <summary>When overridden in a derived class, writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.</summary>
        /// <param name="buffer">An array of bytes. This method copies <paramref name="count" /> bytes from <paramref name="buffer" /> to the current stream. </param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin copying bytes to the current stream. </param>
        /// <param name="count">The number of bytes to be written to the current stream. </param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            _length += count;
        }

        /// <summary>When overridden in a derived class, gets the length in bytes of the stream.</summary>
        /// <returns>A long value representing the length of the stream in bytes.</returns>
        public override long Length => _length;

        /// <summary>When overridden in a derived class, clears all buffers for this stream and causes any buffered data to be written to the underlying device.</summary>
        public override void Flush()
        {
            // Do Nothing
        }

        /// <summary>When overridden in a derived class, sets the position within the current stream.</summary>
        /// <returns>The new position within the current stream.</returns>
        /// <param name="offset">A byte offset relative to the <paramref name="origin" /> parameter. </param>
        /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin" /> indicating the reference point used to obtain the new position. </param>
        public override long Seek(long offset, SeekOrigin origin)
        {
            return -1;
        }

        /// <summary>
        /// When overridden in a derived class, sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        /// <exception cref="InvalidOperationException"></exception>
        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        /// <summary>When overridden in a derived class, reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.</summary>
        /// <returns>The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.</returns>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset" /> and (<paramref name="offset" /> + <paramref name="count" /> - 1) replaced by the bytes read from the current source. </param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin storing the data read from the current stream. </param>
        /// <param name="count">The maximum number of bytes to be read from the current stream. </param>
        /// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <summary>When overridden in a derived class, gets a value indicating whether the current stream supports reading.</summary>
        /// <returns>true if the stream supports reading; otherwise, false.</returns>
        public override bool CanRead => false;

        /// <summary>When overridden in a derived class, gets a value indicating whether the current stream supports seeking.</summary>
        /// <returns>true if the stream supports seeking; otherwise, false.</returns>
        public override bool CanSeek => false;

        /// <summary>When overridden in a derived class, gets a value indicating whether the current stream supports writing.</summary>
        /// <returns>true if the stream supports writing; otherwise, false.</returns>
        public override bool CanWrite => true;

        /// <summary>When overridden in a derived class, gets or sets the position within the current stream.</summary>
        /// <returns>The current position within the stream.</returns>
        public override long Position { get; set; }
    }

    #endregion

}
