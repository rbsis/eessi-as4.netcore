using System.Collections.Concurrent;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Eu.EDelivery.AS4.Model.Core;

namespace Eu.EDelivery.AS4.Serialization;

/// <summary>
/// <see cref="AS4Message" /> Serializer to Xml.
/// </summary>
public static class AS4XmlSerializer
{
    private static readonly IDictionary<Type, XmlSerializer> _serializers =
        new ConcurrentDictionary<Type, XmlSerializer>();

    /// <summary>
    /// Serialize a given data Model to a Xml Stream.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data">The data.</param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public static async Task<Stream> ToStreamAsync<T>(T data, CancellationToken cancellation)
    {
        return await Task.Run(() =>
        {
            var xml = ToString(data);
            return new MemoryStream(Encoding.UTF8.GetBytes(xml));
        }, cancellation);
    }

    /// <summary>
    /// Serialize Model into Xml String
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data">The data.</param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public static async Task<string> ToStringAsync<T>(T data, CancellationToken cancellation)
    {
        return await Task.Run(() => ToString(data), cancellation);
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        private static readonly Encoding _utf8Encoding = new UTF8Encoding(false);

        /// <inheritdoc />
        /// <summary>Gets the <see cref="T:System.Text.Encoding" /> in which the output is written.</summary>
        /// <returns>The Encoding in which the output is written.</returns>
        public override Encoding Encoding => _utf8Encoding;
    }

    /// <summary>
    /// Serialize Model into Xml String
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data"></param>
    /// <returns></returns>
    public static string ToString<T>(T data)
    {
        using var writer = new Utf8StringWriter();
        var serializer = new XmlSerializer(typeof(T));
        serializer.Serialize(writer, data);

        return writer.ToString();
    }

    /// <summary>
    /// To the document.
    /// </summary>
    /// <param name="message">The message.</param>
    /// 
    /// <returns></returns>
    public static XmlDocument ToSoapEnvelopeDocument(AS4Message message)
    {
        return SerializeToSoapEnvelope(message, LoadEnvelopeToDocument);
    }

    private static XmlDocument LoadEnvelopeToDocument(Stream envelopeStream)
    {
        envelopeStream.Position = 0;
        var envelopeXmlDocument = new XmlDocument { PreserveWhitespace = true };

        envelopeXmlDocument.Load(envelopeStream);

        return envelopeXmlDocument;
    }

    /// <summary>
    /// Tries to XML bytes asynchronous.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data">The data.</param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public static async Task<byte[]> TryToXmlBytesAsync<T>(T data, CancellationToken cancellation)
    {
        try
        {
            var xml = await ToStringAsync(data, cancellation);
            return Encoding.UTF8.GetBytes(xml);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            return [];
        }
    }

    /// <summary>
    /// To the SOAP envelope bytes.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public static async Task<byte[]> ToSoapEnvelopeBytesAsync(AS4Message message, CancellationToken cancellation)
    {
        return await Task.Run(() => SerializeToSoapEnvelope(message, s => s.ToArray()), cancellation);
    }

    private static T SerializeToSoapEnvelope<T>(
        AS4Message message,
        Func<MemoryStream, T> handling)
    {
        using var messageStream = new MemoryStream();
        var serializer = new SoapEnvelopeSerializer();
        serializer.Serialize(message, messageStream);

        return handling(messageStream);
    }

    /// <summary>
    /// Deserialize a Xml stream to a Model
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="stream">The stream.</param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public static async Task<T?> FromStreamAsync<T>(Stream stream, CancellationToken cancellation) where T : class
    {
        using var streamReader = new StreamReader(stream);
        stream.Position = 0;

        var xml = await streamReader.ReadToEndAsync(cancellation);
        return await FromStringAsync<T>(xml, cancellation);
    }

    /// <summary>
    /// Deserialize Xml String to Model.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="xml">The XML.</param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public static async Task<T?> FromStringAsync<T>(string? xml, CancellationToken cancellation) where T : class =>
        await Task.Run(() => FromString<T>(xml), cancellation);

    /// <summary>
    /// Deserialize Xml String to Model
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="xml"></param>
    /// <returns></returns>
    public static T? FromString<T>(string? xml) where T : class
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        using var reader = XmlReader.Create(new StringReader(xml));
        var serializer = new XmlSerializer(typeof(T));
        if (serializer.CanDeserialize(reader))
        {
            return serializer.Deserialize(reader) as T;
        }

        return null;
    }

    /// <summary>
    /// Deserialize to a given type from a given reader.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="reader">The reader.</param>
    /// <returns></returns>
    public static async Task<T?> FromReaderAsync<T>(XmlReader reader) where T : class =>
        await Task.Run(() => GetSerializerForType(typeof(T)).Deserialize(reader) as T);

    private static XmlSerializer GetSerializerForType(Type type)
    {
        if (!_serializers.TryGetValue(type, out var value))
        {
            value = new XmlSerializer(type);
            _serializers[type] = value;
        }

        return value;
    }
}
