using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class BidWithItemDto
{
    [Key(0)] public string Id { get; set; }
    [Key(1)] public string BidderUserId { get; set; }
    [Key(2)] public long BidPrice { get; set; }
    [Key(3)] public int BidAmount { get; set; }
    [Key(4)] public DateTime CreatedAt { get; set; }
    [Key(5)] public ItemDto Item { get; set; }

    public BidWithItemDto(string id, string bidderUserId, long bidPrice, int bidAmount, DateTime createdAt,
        ItemDto item)
    {
        Id = id;
        BidderUserId = bidderUserId;
        BidPrice = bidPrice;
        BidAmount = bidAmount;
        CreatedAt = createdAt;
        Item = item;
    }
}
