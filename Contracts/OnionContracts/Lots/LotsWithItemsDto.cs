using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public  class LotsWithItemsDto
{
    [Key(0)] public List<LotWithItemDto> Lots { get; set; }

    public LotsWithItemsDto(List<LotWithItemDto> lots)
    {
        Lots = lots;
    }
}
