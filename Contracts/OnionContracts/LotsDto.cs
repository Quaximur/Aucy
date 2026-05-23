using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public  class LotsDto
{
    [Key(0)] public IList<LotDto> Lots { get; set; }

    public LotsDto(IList<LotDto> lots)
    {
        Lots = lots;
    }
}
