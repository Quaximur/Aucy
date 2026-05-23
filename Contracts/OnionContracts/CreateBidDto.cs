using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public class CreateBidDto
{
    [Key(0)] public string LotId { get; set; }
    [Key(1)] public string BidderUserId { get; set; }
    [Key(2)] public ulong BidPrice { get; set; }
    [Key(3)] public int BidAmount { get; set; }

    public CreateBidDto(string lotId, string bidderUserId, ulong bidPrice, int bidAmount)
    {
        LotId = lotId;
        BidderUserId = bidderUserId;
        BidPrice = bidPrice;
        BidAmount = bidAmount;
    }
}