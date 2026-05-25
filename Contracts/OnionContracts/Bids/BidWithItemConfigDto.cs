using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class BidWithItemConfigDto
{
    [Key(0)] public string Id { get; set; }
    [Key(1)] public string BidderUserId { get; set; }
    [Key(2)] public long BidPrice { get; set; }
    [Key(3)] public int BidAmount { get; set; }
    [Key(4)] public DateTime CreatedAt { get; set; }
    [Key(5)] public ItemConfigDto ItemConfig { get; set; }

    public BidWithItemConfigDto(string id, string bidderUserId, long bidPrice, int bidAmount, DateTime createdAt,
        ItemConfigDto itemConfig)
    {
        Id = id;
        BidderUserId = bidderUserId;
        BidPrice = bidPrice;
        BidAmount = bidAmount;
        CreatedAt = createdAt;
        ItemConfig = itemConfig;
    }
}
