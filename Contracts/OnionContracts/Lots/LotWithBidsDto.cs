using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class LotWithBidsDto
{
    [Key(0)] public string Id { get; set; }
    [Key(1)] public string OwnerUserId { get; set; }
    [Key(2)] public ItemConfigDto ItemConfig { get; set; }
    [Key(3)] public long ItemConfigPrice { get; set; }
    [Key(4)] public int ItemConfigAmount { get; set; }
    [Key(5)] public DateTime CreatedAt { get; set; }
    [Key(6)] public List<BidDto> Bids { get; set; }

    public LotWithBidsDto(string id, string ownerUserId, ItemConfigDto itemConfig, long itemConfigPrice, int itemConfigAmount,
        DateTime createdAt, List<BidDto> bids)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        ItemConfig = itemConfig;
        ItemConfigPrice = itemConfigPrice;
        ItemConfigAmount = itemConfigAmount;
        CreatedAt = createdAt;
        Bids = bids;
    }
}
