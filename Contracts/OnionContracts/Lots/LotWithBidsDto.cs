using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class LotWithBidsDto
{
    [Key(0)] public string Id { get; set; }
    [Key(1)] public string OwnerUserId { get; set; }
    [Key(2)] public ItemDto Item { get; set; }
    [Key(3)] public long ItemPrice { get; set; }
    [Key(4)] public int ItemAmount { get; set; }
    [Key(5)] public DateTime CreatedAt { get; set; }
    [Key(6)] public List<BidDto> Bids { get; set; }

    public LotWithBidsDto(string id, string ownerUserId, ItemDto item, long itemPrice, int itemAmount,
        DateTime createdAt, List<BidDto> bids)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Item = item;
        ItemPrice = itemPrice;
        ItemAmount = itemAmount;
        CreatedAt = createdAt;
        Bids = bids;
    }
}
