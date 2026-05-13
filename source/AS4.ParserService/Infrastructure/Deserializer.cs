using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Serialization;

namespace AS4.ParserService.Infrastructure;

internal static class Deserializer
{
    internal static async Task<SendingProcessingMode?> ToSendingPModeAsync(byte[] value, CancellationToken cancellation)
    {
        using var stream = new MemoryStream(value);
        return await AS4XmlSerializer.FromStreamAsync<SendingProcessingMode>(stream, cancellation);
    }

    internal static async Task<ReceivingProcessingMode?> ToReceivingPModeAsync(byte[] value, CancellationToken cancellation)
    {
        using var stream = new MemoryStream(value);
        return await AS4XmlSerializer.FromStreamAsync<ReceivingProcessingMode>(stream, cancellation);
    }
}
