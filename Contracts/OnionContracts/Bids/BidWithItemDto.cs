using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class BidWithItemDto
{
    [Key(0)] public string Id { get; set; }
    [Key(1)] public string LotId { get; set; }
    [Key(2)] public string BidderUserId { get; set; }
    [Key(3)] public long BidPrice { get; set; }
    [Key(4)] public int BidAmount { get; set; }
    [Key(5)] public DateTime CreatedAt { get; set; }
    [Key(6)] public LotDto Lot { get; set; }

    public BidWithItemDto(string id, string lotId, string bidderUserId, long bidPrice, int bidAmount, DateTime createdAt, 
        LotDto lotDto)
    {
        Id = id;
        LotId = lotId;
        BidderUserId = bidderUserId;
        BidPrice = bidPrice;
        BidAmount = bidAmount;
        CreatedAt = createdAt;
        Lot = lotDto;
    }
}
