using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Serialization;

namespace AS4.ParserService.Infrastructure;

internal static class Serializer
{
    public static byte[] ToByteArray(this ISerializerProvider serializerProvider, AS4Message message)
    {
        if (message == null)
        {
            return [];
        }

        using var stream = new MemoryStream();
        var serializer = serializerProvider.Get(message.ContentType);
        serializer.Serialize(message, stream);

        return stream.ToArray();
    }
}
