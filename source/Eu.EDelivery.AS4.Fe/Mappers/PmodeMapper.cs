using Eu.EDelivery.AS4.Fe.Pmodes.Model;

namespace Eu.EDelivery.AS4.Fe.Mappers;

public class PmodeMapper :
    IMapper<ReceivingBasePmode, ReceivingBasePmode>,
    IMapper<SendingBasePmode, SendingBasePmode>
{
    public ReceivingBasePmode Map(ReceivingBasePmode source) => new()
    {
        Name = source.Name,
        Pmode = source.Pmode,
        Type = source.Type,
        Hash = source.Hash,
    };

    public SendingBasePmode Map(SendingBasePmode source) => new()
    {
        Name = source.Name,
        Pmode = source.Pmode,
        Type = source.Type,
        Hash = source.Hash,
    };
}
