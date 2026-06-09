using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Fe.Monitor.Model;

namespace Eu.EDelivery.AS4.Fe.Mappers;

/// <summary>
/// Mapper for the monitor
/// </summary>
public class MessageMapper :
    IMapper<InMessage, Message>,
    IMapper<OutMessage, Message>,
    IMapper<InException, ExceptionMessage>,
    IMapper<OutException, ExceptionMessage>
{
    private const int ExceptionLength = 100;

    public Message Map(InMessage source) => new()
    {
        Id = source.Id,
        Status = source.Status,
        EbmsMessageType = source.EbmsMessageType.ToString(),
        Operation = source.Operation.ToString(),
        Direction = Direction.Inbound,
        Mep = source.MEP.ToString(),
        Action = source.Action,
        EbmsMessageId = source.EbmsMessageId,
        EbmsRefToMessageId = source.EbmsRefToMessageId,
        FromParty = source.FromParty,
        ToParty = source.ToParty,
        PMode = source.PMode,
        PModeId = source.PModeId,
        ModificationTime = source.ModificationTime,
        InsertionTime = source.InsertionTime,
        Mpc = source.Mpc,
        IsDuplicate = source.IsDuplicate,
        IsTest = source.IsTest,
        Service = source.Service
    };

    public Message Map(OutMessage source) => new()
    {
        Id = source.Id,
        Status = source.Status,
        EbmsMessageType = source.EbmsMessageType.ToString(),
        Operation = source.Operation.ToString(),
        Direction = Direction.Outbound,
        Mep = source.MEP.ToString(),
        Action = source.Action,
        EbmsMessageId = source.EbmsMessageId,
        EbmsRefToMessageId = source.EbmsRefToMessageId,
        FromParty = source.FromParty,
        ToParty = source.ToParty,
        PMode = source.PMode,
        PModeId = source.PModeId,
        ModificationTime = source.ModificationTime,
        InsertionTime = source.InsertionTime,
        Mpc = source.Mpc,
        IsDuplicate = source.IsDuplicate,
        IsTest = source.IsTest,
        Service = source.Service
    };

    public ExceptionMessage Map(InException source) => new()
    {
        Id = source.Id,
        Operation = source.Operation.ToString(),
        Direction = Direction.Inbound,
        Exception = source.Exception,
        ExceptionShort = string.IsNullOrEmpty(source.Exception)
            ? ""
            : source.Exception[(source.Exception.IndexOf(']') + 1)..].Split('\r', '\n')[0].Length > ExceptionLength
                ? source.Exception.Substring(source.Exception.IndexOf(']') + 1).Split('\r', '\n')[0].Substring(0, ExceptionLength) + "..."
                : source.Exception[(source.Exception.IndexOf(']') + 1)..].Split('\r', '\n')[0],
        HasMessageBody = source.MessageLocation != null && !string.IsNullOrWhiteSpace(source.MessageLocation),
        EbmsRefToMessageId = source.EbmsRefToMessageId,
        PMode = source.PMode,
        PModeId = source.PModeId,
        ModificationTime = source.ModificationTime,
        InsertionTime = source.InsertionTime,
    };

    public ExceptionMessage Map(OutException source) => new()
    {
        Id = source.Id,
        Operation = source.Operation.ToString(),
        Direction = Direction.Outbound,
        Exception = source.Exception,
        ExceptionShort = string.IsNullOrEmpty(source.Exception)
            ? ""
            : source.Exception[(source.Exception.IndexOf(']') + 1)..].Split('\r', '\n')[0].Length > ExceptionLength
                ? source.Exception.Substring(source.Exception.IndexOf(']') + 1).Split('\r', '\n')[0].Substring(0, ExceptionLength) + "..."
                : source.Exception[(source.Exception.IndexOf(']') + 1)..].Split('\r', '\n')[0],
        HasMessageBody = source.MessageLocation != null && !string.IsNullOrWhiteSpace(source.MessageLocation),
        EbmsRefToMessageId = source.EbmsRefToMessageId,
        PMode = source.PMode,
        PModeId = source.PModeId,
        ModificationTime = source.ModificationTime,
        InsertionTime = source.InsertionTime,
    };
}
