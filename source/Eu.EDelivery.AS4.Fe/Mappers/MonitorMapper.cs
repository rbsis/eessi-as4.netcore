using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Fe.Monitor.Model;

namespace Eu.EDelivery.AS4.Fe.Mappers;

/// <summary>
/// Mapper for the monitor
/// </summary>
public class MonitorMapper :
    IMapper<InMessage, Message>,
    IMapper<OutMessage, Message>,
    IMapper<InException, ExceptionMessage>,
    IMapper<OutException, ExceptionMessage>
{
    private const int ExceptionLength = 100;

    public Message Map(InMessage source) => new()
    {
        Status = source.Status,
        EbmsMessageType = source.EbmsMessageType.ToString(),
        Operation = source.Operation.ToString(),
        Direction = Direction.Inbound,
        Mep = source.MEP.ToString()
    };

    public Message Map(OutMessage source) => new()
    {
        Status = source.Status,
        EbmsMessageType = source.EbmsMessageType.ToString(),
        Operation = source.Operation.ToString(),
        Direction = Direction.Outbound,
        Mep = source.MEP.ToString()
    };

    public ExceptionMessage Map(InException source) => new()
    {
        Direction = Direction.Inbound,
        ExceptionShort = string.IsNullOrEmpty(source.Exception)
            ? ""
            : source.Exception[(source.Exception.IndexOf(']') + 1)..].Split('\r', '\n')[0].Length > ExceptionLength
                ? source.Exception.Substring(source.Exception.IndexOf(']') + 1).Split('\r', '\n')[0].Substring(0, ExceptionLength) + "..."
                : source.Exception[(source.Exception.IndexOf(']') + 1)..].Split('\r', '\n')[0],
        HasMessageBody = source.MessageLocation != null && !string.IsNullOrWhiteSpace(source.MessageLocation)
    };

    public ExceptionMessage Map(OutException source) => new()
    {
        Direction = Direction.Outbound,
        ExceptionShort = string.IsNullOrEmpty(source.Exception)
            ? ""
            : source.Exception[(source.Exception.IndexOf(']') + 1)..].Split('\r', '\n')[0].Length > ExceptionLength
                ? source.Exception.Substring(source.Exception.IndexOf(']') + 1).Split('\r', '\n')[0].Substring(0, ExceptionLength) + "..."
                : source.Exception[(source.Exception.IndexOf(']') + 1)..].Split('\r', '\n')[0],
        HasMessageBody = source.MessageLocation != null && !string.IsNullOrWhiteSpace(source.MessageLocation)
    };
}
