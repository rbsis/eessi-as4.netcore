using Eu.EDelivery.AS4.Model.Core;

namespace Eu.EDelivery.AS4.UnitTests.Extensions;

/// <summary>
/// Extension methods for the <see cref="AS4Message"/>
/// </summary>
public static class AS4MessageExtensions
{
    /// <summary>
    /// Serialize a given <paramref name="message"/> to a Stream instance.
    /// </summary>
    /// <param name="message">Given message to serialize.</param>
    /// <returns></returns>
    public static MemoryStream ToStream(this AS4Message message)
    {
        var memoryStream = new MemoryStream();

        var serializer = Default.SerializerProvider.Get(message.ContentType);
        serializer.Serialize(message, memoryStream);

        memoryStream.Position = 0;
        return memoryStream;
    }
}
