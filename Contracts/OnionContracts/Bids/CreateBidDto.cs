using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class CreateBidDto
{
    [Key(0)] public string ItemId { get; set; }
    [Key(1)] public string BidderUserId { get; set; }
    [Key(2)] public long BidPrice { get; set; }
    [Key(3)] public int BidAmount { get; set; }

    public CreateBidDto(string itemId, string bidderUserId, long bidPrice, int bidAmount)
    {
        ItemId = itemId;
        BidderUserId = bidderUserId;
        BidPrice = bidPrice;
        BidAmount = bidAmount;
    }
}