using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public  class LotsDto
{
    [Key(0)] public List<LotDto> Lots { get; set; }

    public LotsDto(List<LotDto> lots)
    {
        Lots = lots;
    }
}
