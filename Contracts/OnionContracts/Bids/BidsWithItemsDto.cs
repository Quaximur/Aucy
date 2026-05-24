using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class  BidsWithItemsDto
{
    [Key(0)] public List<BidWithItemDto> BidsWithItems { get; set; }

    public BidsWithItemsDto(List<BidWithItemDto> bidsWithLots)
    {
        BidsWithItems = bidsWithLots;
    }
}